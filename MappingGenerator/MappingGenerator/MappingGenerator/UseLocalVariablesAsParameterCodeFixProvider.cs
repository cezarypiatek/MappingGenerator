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
            
            var statement = token.Parent.FindContainer<InvocationExpressionSyntax>();
            if (statement == null || statement.ArgumentList.Arguments.Count != 0)
            {
                return;
            }

            context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, statement, false, c), equivalenceKey: title), diagnostic);
            context.RegisterCodeFix(CodeAction.Create(title: titleWithNamed, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, statement, true, c), equivalenceKey: titleWithNamed), diagnostic);
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
                    var root = await document.GetSyntaxRootAsync(cancellationToken);
                    var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
                    var argumentList = parametersMatch.ToArgumentListSyntax(syntaxGenerator, generateNamedParameters);
                    var newRoot = root.ReplaceNode(invocationExpression, invocationExpression.WithArgumentList(argumentList));
                    return document.WithSyntaxRoot(newRoot);
                }
            }
            return document;
        }
    }
}
