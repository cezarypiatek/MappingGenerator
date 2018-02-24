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
            var diagnostic = context.Diagnostics.First();
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            var statement = FindStatementToReplace(token.Parent);
            if (statement == null)
            {
                return;
            }

            switch (statement)
            {
                case AssignmentExpressionSyntax assignmentExpression:
                    context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => GenerateExplicitConversion(context.Document, assignmentExpression, c), equivalenceKey: title), diagnostic);
                    break;
                case ReturnStatementSyntax returnStatement:
                    break;
               
            }
        }

        private async Task<Document> GenerateExplicitConversion(Document document, AssignmentExpressionSyntax assignmentExpression, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var generator = SyntaxGenerator.GetGenerator(document);
            var sourceType = semanticModel.GetTypeInfo(assignmentExpression.Right).Type;
            var destimationType = semanticModel.GetTypeInfo(assignmentExpression.Left).Type;
            //WARN: cheap speaculation, no idea how to deal with it in more generic way
            var targetExists = assignmentExpression.Left.Kind() == SyntaxKind.IdentifierName && semanticModel.GetSymbolInfo(assignmentExpression.Left).Symbol.Kind != SymbolKind.Property;
            var mappingStatements = MappingGenerator.MapTypes(sourceType, destimationType, generator, assignmentExpression.Right, assignmentExpression.Left, targetExists).ToList();
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var assignmentStatement = assignmentExpression.Parent;
            var newRoot = assignmentStatement.Parent.Kind() == SyntaxKind.Block
                ? root.ReplaceNode(assignmentStatement, mappingStatements)
                : root.ReplaceNode(assignmentStatement, SyntaxFactory.Block(mappingStatements.OfType<StatementSyntax>()));
            return document.WithSyntaxRoot(newRoot);
        }

        private SyntaxNode FindStatementToReplace(SyntaxNode node)
        {
            switch (node)
            {
                    case AssignmentExpressionSyntax assignmentStatement:
                        return assignmentStatement;
                    case ReturnStatementSyntax returnStatement:
                        return returnStatement;
                    default:
                        return node.Parent == null? null: FindStatementToReplace(node.Parent);
            }
        }
    }
}
