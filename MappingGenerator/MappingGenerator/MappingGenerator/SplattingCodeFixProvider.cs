using System.Composition;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SplattingCodeFixProvider)), Shared]
    public class SplattingCodeFixProvider : CodeFixProvider
    {
        private const string title = "Generate splatting";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("CS7036");

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
            if (statement == null || statement.ArgumentList.Arguments.Count != 1)
            {
                return;
            }

            context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with value parameters", createChangedDocument: c => GenerateSplatting(context.Document, statement, false, c), equivalenceKey: "Generate splatting with value parameters"), diagnostic);
            context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with named parameters", createChangedDocument: c => GenerateSplatting(context.Document, statement, true, c), equivalenceKey: "Generate splatting with named parameters"), diagnostic);
        }

        private async Task<Document> GenerateSplatting(Document document,  InvocationExpressionSyntax invocationExpression, bool generateNamedParameters, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var methodSymbol = semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
            if (methodSymbol == null)
            {
                return document;
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var invalidArgument = invocationExpression.ArgumentList.Arguments.First();
            var sourceType = semanticModel.GetTypeInfo(invalidArgument.Expression);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var mappingSourceFinder = new ObjectMembersMappingSourceFinder(sourceType.Type, invalidArgument.Expression, syntaxGenerator, semanticModel);
            var parametersMatch = MethodHelper.FindBestParametersMatch(methodSymbol, semanticModel, mappingSourceFinder);
            if (parametersMatch == null)
            {
                return document;
            }

            var argumentList = parametersMatch.ToArgumentListSyntax(syntaxGenerator, generateNamedParameters);
            var newRoot = root.ReplaceNode(invocationExpression, invocationExpression.WithArgumentList(argumentList));
            return document.WithSyntaxRoot(newRoot);
        }


}
}
