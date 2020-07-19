using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Mappings;
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SplattingCodeFixProvider)), Shared]
    public class SplattingCodeFixProvider : CodeFixProvider
    {

        public const string CS7036 = nameof(CS7036);
        public const string CS1501 = nameof(CS1501);

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(CS7036, CS1501);

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
                if (expression is ObjectCreationExpressionSyntax creationExpression)
                {
                    if (creationExpression.ArgumentList?.Arguments.Count == 1)
                    {
                        var invocation = new ConstructorInvocation(creationExpression);
                        context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with value parameters", createChangedDocument: c => GenerateSplatting(context.Document, invocation, false, c), equivalenceKey: "Generate splatting with value parameters"+"_constructor"), diagnostic);
                        context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with named parameters", createChangedDocument: c => GenerateSplatting(context.Document, invocation, true, c), equivalenceKey: "Generate splatting with named parameters"+"_constructor"), diagnostic);
                    }
                }
                else if (expression is InvocationExpressionSyntax invocationExpression)
                {
                    if (invocationExpression.ArgumentList?.Arguments.Count == 1)
                    {
                        var invocation = new MethodInvocation(invocationExpression);
                        context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with value parameters", createChangedDocument: c => GenerateSplatting(context.Document, invocation, false, c), equivalenceKey: "Generate splatting with value parameters"), diagnostic);
                        context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with named parameters", createChangedDocument: c => GenerateSplatting(context.Document, invocation, true, c), equivalenceKey: "Generate splatting with named parameters"), diagnostic);
                    }
                }
            }
        }

        private async Task<Document> GenerateSplatting(Document document, IInvocation invocation, bool generateNamedParameters, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var overloadParameterSets = invocation.GetOverloadParameterSets(semanticModel);
            if (overloadParameterSets != null)
            {
                var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
                var mappingEngine = new MappingEngine(semanticModel, syntaxGenerator);
                var invalidArgumentList = invocation.Arguments;
                var mappingContext = new MappingContext(invocation.SourceNode, semanticModel);
                var parametersMatch = FindParametersMatch(invalidArgumentList, overloadParameterSets, semanticModel, syntaxGenerator, mappingContext);
                if (parametersMatch != null)
                {
                    
                    var argumentList = parametersMatch.ToArgumentListSyntax(mappingEngine, mappingContext, generateNamedParameters);
                    return await document.ReplaceNodes(invocation.SourceNode, invocation.WithArgumentList(argumentList), cancellationToken);
                
                }
            }

            return document;
        }

        private static MatchedParameterList FindParametersMatch(ArgumentListSyntax invalidArgumentList,
            IEnumerable<ImmutableArray<IParameterSymbol>> overloadParameterSets, SemanticModel semanticModel,
            SyntaxGenerator syntaxGenerator, MappingContext mappingContext) 
        {
            var sourceFinder = CreateSourceFinderBasedOnInvalidArgument(invalidArgumentList, semanticModel, syntaxGenerator);
            var parametersMatch = MethodHelper.FindBestParametersMatch(sourceFinder, overloadParameterSets, mappingContext);
            return parametersMatch;
        }

        private static ObjectMembersMappingSourceFinder CreateSourceFinderBasedOnInvalidArgument(ArgumentListSyntax invalidArgumentList, SemanticModel semanticModel, SyntaxGenerator syntaxGenerator)
        {
            var invalidArgument = invalidArgumentList.Arguments.First();
            var sourceType = semanticModel.GetTypeInfo(invalidArgument.Expression).GetAnnotatedType();
            return new ObjectMembersMappingSourceFinder(new AnnotatedType(sourceType.Type), invalidArgument.Expression, syntaxGenerator);
        }
    }
}
