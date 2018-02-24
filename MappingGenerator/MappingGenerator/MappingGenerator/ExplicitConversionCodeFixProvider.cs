using System.Collections.Generic;
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
                    context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => GenerateExplicitConversion(context.Document, returnStatement, c), equivalenceKey: title), diagnostic);
                    break; 
                case YieldStatementSyntax yieldStatement:
                    context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => GenerateExplicitConversion(context.Document, yieldStatement, c), equivalenceKey: title), diagnostic);
                    break;
            }
        }

        private async Task<Document> GenerateExplicitConversion(Document document, AssignmentExpressionSyntax assignmentExpression, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var mappingGenerator = new MappingGenerator(syntaxGenerator);
            var sourceType = semanticModel.GetTypeInfo(assignmentExpression.Right).Type;
            var destimationType = semanticModel.GetTypeInfo(assignmentExpression.Left).Type;
            //WARN: cheap speaculation, no idea how to deal with it in more generic way
            var targetExists = assignmentExpression.Left.Kind() == SyntaxKind.IdentifierName && semanticModel.GetSymbolInfo(assignmentExpression.Left).Symbol.Kind != SymbolKind.Property;
            var mappingStatements = mappingGenerator.MapTypes(sourceType, destimationType, assignmentExpression.Right, assignmentExpression.Left, targetExists: targetExists).ToList();
            var assignmentStatement = assignmentExpression.Parent;
            return await ReplaceStatement(document, assignmentStatement, mappingStatements, cancellationToken);
        }

        private async Task<Document> GenerateExplicitConversion(Document document, ReturnStatementSyntax returnStatement, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var mappingGenerator = new MappingGenerator(syntaxGenerator);
            var returnExpressionType = semanticModel.GetTypeInfo(returnStatement.Expression);
            var mappingStatements = mappingGenerator.MapTypes(returnExpressionType.Type, returnExpressionType.ConvertedType, returnStatement.Expression);
            return await ReplaceStatement(document, returnStatement, mappingStatements, cancellationToken);
        }

        private async Task<Document> GenerateExplicitConversion(Document document, YieldStatementSyntax yieldStatement, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var mappingGenerator = new MappingGenerator(syntaxGenerator);
            var yieldExpressionType = semanticModel.GetTypeInfo(yieldStatement.Expression);
            var mappingStatements = mappingGenerator.MapTypes(yieldExpressionType.Type, yieldExpressionType.ConvertedType, yieldStatement.Expression, generatorContext: true);
            return await ReplaceStatement(document, yieldStatement, mappingStatements, cancellationToken);
        }

        private static async Task<Document> ReplaceStatement(Document document, SyntaxNode statement, IEnumerable<SyntaxNode> newStatements, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = statement.Parent.Kind() == SyntaxKind.Block
                ? root.ReplaceNode(statement, newStatements)
                : root.ReplaceNode(statement, SyntaxFactory.Block(newStatements.OfType<StatementSyntax>()));
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
                    case YieldStatementSyntax yieldStatement:
                        return yieldStatement;
                    default:
                        return node.Parent == null? null: FindStatementToReplace(node.Parent);
            }
        }
    }
}
