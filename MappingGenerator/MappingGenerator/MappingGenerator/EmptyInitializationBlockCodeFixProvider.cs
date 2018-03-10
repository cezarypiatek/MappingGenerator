using System.Composition;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator
{

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyInitializationBlockCodeFixProvider)), Shared]
    public class EmptyInitializationBlockCodeFixProvider : CodeFixProvider
    {
        private const string title = "Initialize with local variables";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EmptyInitializationBlockAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            var objectInitializer = token.Parent.FindContainer<InitializerExpressionSyntax>();
            if (objectInitializer == null)
            {
                return;
            }
            context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => InitizalizeWithLocals(context.Document, objectInitializer, c), equivalenceKey: title), diagnostic);
        }

        private async Task<Document> InitizalizeWithLocals(Document document, InitializerExpressionSyntax objectInitializer, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var objectCreationExpression = objectInitializer.FindContainer<ObjectCreationExpressionSyntax>();
            var createdObjectType = ModelExtensions.GetTypeInfo(semanticModel, objectCreationExpression).Type;
            var mappingSourceFinder = new LocalScopeMappingSourceFinder(semanticModel, objectInitializer);
            var propertiesToSet = ObjectHelper.GetPublicPropertySymbols(createdObjectType).Where(x=> x.SetMethod.DeclaredAccessibility == Accessibility.Public);
            var initExpressions = propertiesToSet.Aggregate(objectInitializer.Expressions, (expr, property) =>
            {
                var mappingSource = mappingSourceFinder.FindMappingSource(property.Name, property.Type);
                if (mappingSource != null)
                {
                    var assignmentExpression = SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,SyntaxFactory.IdentifierName(property.Name), mappingSource.Expression);
                    return expr.Add(assignmentExpression);
                }
                return expr;
            });
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(objectInitializer, objectInitializer.WithExpressions(initExpressions));
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
