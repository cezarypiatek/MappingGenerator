using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Mappings;
using MappingGenerator.Mappings.SourceFinders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.Features.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExplicitConversionCodeFixProvider)), Shared]
    public class ExplicitConversionCodeFixProvider : CodeFixProvider
    {
        private const string title = "Generate explicit conversion";
        public const string CS0029 = nameof(CS0029);
        public const string CS0266 = nameof(CS0266);

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create( CS0029, CS0266); }
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
                    context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => GenerateExplicitConversion(context.Document, returnStatement, c), equivalenceKey: title), diagnostic);
                    break; 
                case YieldStatementSyntax yieldStatement:
                    context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => GenerateExplicitConversion(context.Document, yieldStatement, c), equivalenceKey: title), diagnostic);
                    break;
            }
        }

        private async Task<Document> GenerateExplicitConversion(Document document, AssignmentExpressionSyntax assignmentExpression, CancellationToken cancellationToken)
        {
            var (mappingEngine, semanticModel) = await CreateMappingEngine(document, assignmentExpression, cancellationToken);
            var sourceType = mappingEngine.GetExpressionTypeInfo(assignmentExpression.Right).GetAnnotatedType();
            var destinationType = mappingEngine.GetExpressionTypeInfo(assignmentExpression.Left).GetAnnotatedType();
            var mappingExpression = mappingEngine.MapExpression(assignmentExpression.Right.WithoutTrivia(), sourceType, destinationType, new MappingContext(assignmentExpression, semanticModel)); 
            return await ReplaceNode(document, assignmentExpression, assignmentExpression.WithRight(mappingExpression), cancellationToken);
        }

        private static async Task<(MappingEngine, SemanticModel)> CreateMappingEngine(Document document, SyntaxNode node, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var mappingEngine = await MappingEngine.Create(document, cancellationToken);
            return (mappingEngine, semanticModel);
        }

        private async Task<Document> GenerateExplicitConversion(Document document, ReturnStatementSyntax returnStatement, CancellationToken cancellationToken)
        {
            var (mappingEngine, semanticModel) = await CreateMappingEngine(document, returnStatement, cancellationToken);
            var returnExpressionTypeInfo = mappingEngine.GetExpressionTypeInfo(returnStatement.Expression);
            var mappingExpression = mappingEngine.MapExpression(returnStatement.Expression!.WithoutTrivia(), returnExpressionTypeInfo.GetAnnotatedType(), returnExpressionTypeInfo.GetAnnotatedTypeForConverted(), new MappingContext(returnStatement, semanticModel)); 
            return await ReplaceNode(document, returnStatement, returnStatement.WithExpression(mappingExpression), cancellationToken);
        }

        private async Task<Document> GenerateExplicitConversion(Document document, YieldStatementSyntax yieldStatement, CancellationToken cancellationToken)
        {
            var (mappingEngine, semanticModel) = await CreateMappingEngine(document, yieldStatement, cancellationToken);
            var returnExpressionTypeInfo = mappingEngine.GetExpressionTypeInfo(yieldStatement.Expression);
            var mappingExpression = mappingEngine.MapExpression(yieldStatement.Expression!.WithoutTrivia(), returnExpressionTypeInfo.GetAnnotatedType(), returnExpressionTypeInfo.GetAnnotatedTypeForConverted(), new MappingContext(yieldStatement, semanticModel)); 
            return await ReplaceNode(document, yieldStatement, yieldStatement.WithExpression(mappingExpression), cancellationToken);
        }

        private static async Task<Document> ReplaceNode(Document document, SyntaxNode oldNode, SyntaxNode newNode, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root!.ReplaceNode(oldNode, newNode);
            return document.WithSyntaxRoot(newRoot);
        }

        private SyntaxNode FindStatementToReplace(SyntaxNode node)
        {
            switch (node)
            {
                //TODO EqualsValueClauseSyntax - Type1 v = vOfType2
                    case AssignmentExpressionSyntax assignmentStatement:
                        return assignmentStatement;
                    case ReturnStatementSyntax returnStatement:
                        return returnStatement;
                    case YieldStatementSyntax yieldStatement:
                        return yieldStatement;
                    default:
                        return node.Parent == null? null: FindStatementToReplace(node.Parent);
            }
        }
    }
}
