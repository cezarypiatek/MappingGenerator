using System.Composition;
using System.Collections.Immutable;
using System.Linq;
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExplicitConversionCodeFixProvider)), Shared]
    public class ExplicitConversionCodeFixProvider : CodeFixProvider
    {
        public const string DiagnosticId = "CS0029";
        private const string title = "Generate explicit conversion";
    
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start);

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => GenerateExplicitConversion(context.Document, declaration, c), 
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Document> GenerateExplicitConversion(Document document, SyntaxToken culprit, CancellationToken cancellationToken)
        {
            var statement = FindStatementToReplace(culprit.Parent);
            if (statement == null)
            {
                return document;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var generator = SyntaxGenerator.GetGenerator(document);

            switch (statement)
            {
                case AssignmentExpressionSyntax assignmentStatement:
                    var sourceType = semanticModel.GetTypeInfo(assignmentStatement.Right).Type;
                    var destimationType = semanticModel.GetTypeInfo(assignmentStatement.Left).Type;
                    var mappingStatements = MappingGenerator.MapTypes(sourceType, destimationType, generator, assignmentStatement.Right, assignmentStatement.Left).ToList();

                    var root = await document.GetSyntaxRootAsync(cancellationToken);
                    var syntaxEditor = new SyntaxEditor(root, document.Project.Solution.Workspace);
                    syntaxEditor.InsertAfter(assignmentStatement, mappingStatements);
                    syntaxEditor.RemoveNode(assignmentStatement);
                    var newRoot = syntaxEditor.GetChangedRoot();
                    return document.WithSyntaxRoot(newRoot);

                case ReturnStatementSyntax returnStatement:
                    return document;
                default:
                    return document;
            }
        }

        private SyntaxNode FindStatementToReplace(SyntaxNode culprit)
        {
            switch (culprit)
            {
                    case AssignmentExpressionSyntax assignmentStatement:
                        return assignmentStatement;
                    case ReturnStatementSyntax returnStatement:
                        return returnStatement;
                    default:
                        return culprit.Parent == null? null: FindStatementToReplace(culprit.Parent);
            }
        }
    }
}
