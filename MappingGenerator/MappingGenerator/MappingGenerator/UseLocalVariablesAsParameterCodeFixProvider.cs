using System;
using System.Composition;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseLocalVariablesAsParameterCodeFixProvider)), Shared]
    public class UseLocalVariablesAsParameterCodeFixProvider : CodeFixProvider
    {
        private const string title = "Use local variables as parameters";
        private const string titleWithNamed = "Use local variables as named parameters";
        
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("CS1501", "CS7036");

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            
            var invocationExpression = token.Parent.FindContainer<InvocationExpressionSyntax>();
            if (invocationExpression != null && invocationExpression.ArgumentList.Arguments.Count == 0)
            {
                context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, invocationExpression, false, c), equivalenceKey: title), diagnostic);
                context.RegisterCodeFix(CodeAction.Create(title: titleWithNamed, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, invocationExpression, true, c), equivalenceKey: titleWithNamed), diagnostic);
                return;
            }

            var creationExpression = token.Parent.FindContainer<ObjectCreationExpressionSyntax>();
            if (creationExpression != null && creationExpression.ArgumentList.Arguments.Count == 0)
            {
                context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, creationExpression, false, c), equivalenceKey: title+"for constructor"), diagnostic);
                context.RegisterCodeFix(CodeAction.Create(title: titleWithNamed, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, creationExpression, true, c), equivalenceKey: titleWithNamed+"for constructor"), diagnostic);
            }
        }

        private async Task<Document> UseLocalVariablesAsParameters(Document document, InvocationExpressionSyntax invocationExpression, bool generateNamedParameters, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var methodSymbol = semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
            if (methodSymbol != null)
            {
                var mappingSourceFinder = new LocalScopeMappingSourceFinder(semanticModel, invocationExpression);
                var parametersMatch = MethodHelper.FindBestParametersMatch(methodSymbol, semanticModel, mappingSourceFinder);
                if (parametersMatch != null)
                {
                    var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
                    var argumentList = parametersMatch.ToArgumentListSyntax(syntaxGenerator, generateNamedParameters);
                    return await  document.ReplaceNodes(invocationExpression, invocationExpression.WithArgumentList(argumentList), cancellationToken);
                }
            }
            return document;
        }

        private async Task<Document> UseLocalVariablesAsParameters(Document document, ObjectCreationExpressionSyntax creationExpression, bool generateNamedParameters, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var mappingSourceFinder = new LocalScopeMappingSourceFinder(semanticModel, creationExpression);
            var instantiatedType = (INamedTypeSymbol)semanticModel.GetSymbolInfo(creationExpression.Type).Symbol;
            var overloadParameterSets = instantiatedType.Constructors.Select(x => x.Parameters);

            var parametersMatch = MethodHelper.FindBestParametersMatch(mappingSourceFinder, overloadParameterSets);
            if (parametersMatch != null)
            {
                var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
                var argumentList = parametersMatch.ToArgumentListSyntax(syntaxGenerator, generateNamedParameters);
                return await  document.ReplaceNodes(creationExpression, creationExpression.WithArgumentList(argumentList), cancellationToken);
            }
            return document;
        }
    }
}
