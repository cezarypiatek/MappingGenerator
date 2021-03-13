using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.MethodHelpers;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Features.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InvocationScaffoldingCodeFixProvider)), Shared]
    public class InvocationScaffoldingCodeFixProvider : CodeFixProvider
    {
        public const string CS7036 = nameof(CS7036);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(CS7036);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            var invocationExpression = token.Parent.FindNearestContainer<InvocationExpressionSyntax, ObjectCreationExpressionSyntax>();
            var invocation = GetInvocation(invocationExpression);
            if (invocation is null)
            {
                return;
            }
            context.RegisterCodeFix(CodeAction.Create("Scaffold invocation (regular arguments)",c => ScaffoldInvocation(context.Document, invocation, namedArguments: false, cancellationToken: c), equivalenceKey: "Scaffold invocation (regular arguments)"), diagnostic);
            context.RegisterCodeFix(CodeAction.Create("Scaffold invocation (named arguments)", c => ScaffoldInvocation(context.Document, invocation, namedArguments: true, cancellationToken: c), equivalenceKey: "Scaffold invocation (named arguments)"), diagnostic);
        }

        private async Task<Document> ScaffoldInvocation(Document document, IInvocation invocation, bool namedArguments,
            CancellationToken cancellationToken)
        {
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var mappingSourceFinder = new ScaffoldingSourceFinder(syntaxGenerator,document);
            return await CodeFixHelper.FixInvocationWithParameters(document, invocation, namedArguments, semanticModel, mappingSourceFinder, cancellationToken).ConfigureAwait(false);
        }

        private static IInvocation GetInvocation(SyntaxNode invocationExpression)
        {
            switch (invocationExpression)
            {
                case InvocationExpressionSyntax ie:
                    return new MethodInvocation(ie);
                case ObjectCreationExpressionSyntax oce:
                    return new ConstructorInvocation(oce);
                default:
                    return null;
            }
        }
    }
}
