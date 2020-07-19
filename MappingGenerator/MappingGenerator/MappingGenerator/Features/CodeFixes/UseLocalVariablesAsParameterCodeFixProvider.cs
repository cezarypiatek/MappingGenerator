using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Mappings;
using MappingGenerator.Mappings.MappingImplementors;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.MethodHelpers;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Features.CodeFixes
{
    public class CodeFixHelper
    {
        public static async Task<Document> FixInvocationWithParameters(Document document,
            IInvocation invocation,
            bool generateNamedParameters,
            SemanticModel semanticModel,
            IMappingSourceFinder mappingSourceFinder,
            CancellationToken cancellationToken)
        {
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var overloadParameterSets = invocation.GetOverloadParameterSets(semanticModel);
            if (overloadParameterSets != null)
            {
                var mappingContext = new MappingContext(invocation.SourceNode, semanticModel);
                var mappingEngine = new MappingEngine(semanticModel, syntaxGenerator);
                var parametersMatch = MethodHelper.FindBestParametersMatch(mappingSourceFinder, overloadParameterSets, mappingContext);
                if (parametersMatch != null)
                {
                    
                    var argumentList = parametersMatch.ToArgumentListSyntax(mappingEngine, mappingContext, generateNamedParameters);
                    return await document.ReplaceNodes(invocation.SourceNode, invocation.WithArgumentList(argumentList), cancellationToken);
                }
            }

            return document;
        }
    }

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseLocalVariablesAsParameterCodeFixProvider)), Shared]
    public class UseLocalVariablesAsParameterCodeFixProvider : CodeFixProvider
    {
        private const string title = "Use local variables as parameters";
        private const string titleWithNamed = "Use local variables as named parameters";
        private const string titleWitSelect = "Create mapping lambda";

        /// <summary>
        /// No overload for method 'method' takes 'number' arguments
        /// </summary>
        public const string CS1501 = nameof(CS1501);
        
        
        /// <summary>
        /// There is no argument given that corresponds to the required formal parameter 
        /// </summary>
        public const string CS7036 = nameof(CS7036);
        
        /// <summary>
        /// type' does not contain a constructor that takes 'number' arguments.
        /// </summary>
        public const string CS1729 = nameof(CS1729);

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(CS1501, CS7036, CS1729);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            
            var expression = token.Parent.FindNearestContainer<InvocationExpressionSyntax, ObjectCreationExpressionSyntax>();
            if (expression != null)
            {
                if (expression is InvocationExpressionSyntax invocationExpression)
                {
                    if (invocationExpression.ArgumentList.Arguments.Count == 0)
                    {
                        var invocation = new MethodInvocation(invocationExpression);
                        if (invocationExpression.Expression is MemberAccessExpressionSyntax mae)
                        {
                            var methodName = mae.Name.ToFullString();
                            if (methodName == "Select" || methodName == "ConvertAll")
                            {
                                context.RegisterCodeFix(CodeAction.Create(title: titleWitSelect, createChangedDocument: c => CreateMappingLambda(context.Document, invocationExpression,  c), equivalenceKey: titleWitSelect), diagnostic);
                            }
                        }
                        else
                        {
                            context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, invocation, false, c), equivalenceKey: title), diagnostic);
                            context.RegisterCodeFix(CodeAction.Create(title: titleWithNamed, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, invocation, true, c), equivalenceKey: titleWithNamed), diagnostic);
                        }
                    }
                }else if (expression is ObjectCreationExpressionSyntax creationExpression)
                {
                    if (creationExpression.ArgumentList?.Arguments.Count == 0)
                    {
                        var invocation = new ConstructorInvocation(creationExpression);
                        context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, invocation, false, c), equivalenceKey: title+"for constructor"), diagnostic);
                        context.RegisterCodeFix(CodeAction.Create(title: titleWithNamed, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, invocation, true, c), equivalenceKey: titleWithNamed+"for constructor"), diagnostic);
                    }
                }
            }
        }

        private static async Task<Document> CreateMappingLambda(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var methodInvocationSymbol = semanticModel.GetSymbolInfo(invocation.Expression);
            var mappingOverload = methodInvocationSymbol.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault(IsMappingMethod);
            if (mappingOverload == null)
            {
                return document;
            }

            var parameterType = ((INamedTypeSymbol)mappingOverload.Parameters[0].Type);
            var sourceElementType = parameterType.TypeArguments[0];
            var targetElementType = GetExpressionType(semanticModel, invocation);
            if (targetElementType == null)
            {
                return document;
            }
            
            var mappingEngine = new MappingEngine(semanticModel, syntaxGenerator);
            var sourceListElementType = new AnnotatedType(sourceElementType);
            var mappingLambda = mappingEngine.CreateMappingLambda("x", sourceListElementType, targetElementType, new MappingPath(), new MappingContext(invocation, semanticModel));
            return await document.ReplaceNodes(invocation, invocation.WithArgumentList(SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument((ExpressionSyntax)mappingLambda))), cancellationToken);
        }

        private static bool IsMappingMethod(IMethodSymbol c)
        {
            if (c.Parameters.Length == 1 && c.Parameters[0].Type.Name == "Func")
            {
                return true;
            }

            if (c.Parameters.Length == 1 && c.Parameters[0].Type.Name == "Converter")
            {
                return true;
            }

            return false;
        }

        private static AnnotatedType GetExpressionType(SemanticModel semanticModel, SyntaxNode sourceNodeParent)
        {
            if (sourceNodeParent == null)
            {
                return null;
            }

            if (sourceNodeParent is MethodDeclarationSyntax methodDeclaration)
            {
                var returnTypeInfo = semanticModel.GetTypeInfo(methodDeclaration.ReturnType);
                if (returnTypeInfo.Type != null && MappingHelper.IsCollection(returnTypeInfo.Type))
                {
                    return MappingHelper.GetElementType(returnTypeInfo.Type);
                }

                return null;
            }

            if (sourceNodeParent is LocalDeclarationStatementSyntax localDeclaration && localDeclaration.Declaration.Type.IsVar)
            {
                return null;
            }

            var typeInfo = semanticModel.GetTypeInfo(sourceNodeParent);
            if (typeInfo.ConvertedType != null && typeInfo.ConvertedType.Kind != SymbolKind.ErrorType && typeInfo.ConvertedType is INamedTypeSymbol nt && nt.TypeArguments.Length > 0 && nt.TypeArguments[0] is INamedTypeSymbol)
            {
                return new AnnotatedType(nt.TypeArguments[0]);
            }
            return GetExpressionType(semanticModel, sourceNodeParent.Parent);
        }

        private async Task<Document> UseLocalVariablesAsParameters(Document document, IInvocation invocation, bool generateNamedParameters, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var mappingSourceFinder =  LocalScopeMappingSourceFinder.FromScope(semanticModel, invocation.SourceNode);
            return await CodeFixHelper.FixInvocationWithParameters(document, invocation, generateNamedParameters, semanticModel, mappingSourceFinder, cancellationToken);
        }
    }
}
