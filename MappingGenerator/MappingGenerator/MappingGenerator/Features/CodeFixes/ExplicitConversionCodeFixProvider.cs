using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Mappings;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Features.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExplicitConversionCodeFixProvider)), Shared]
    public class ExplicitConversionCodeFixProvider : CodeFixProvider
    {
        private const string title = "Generate explicit conversion";
        private const string title2 = "Generate explicit conversion (Try re-use instance)";
        public const string CS0029 = nameof(CS0029);
        public const string CS0266 = nameof(CS0266);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(CS0029, CS0266);

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
                    context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => GenerateExplicitConversion(context.Document, assignmentExpression, false, c), equivalenceKey: title), diagnostic);

                    if (assignmentExpression.Parent is InitializerExpressionSyntax || assignmentExpression.Parent is StatementSyntax)
                    {
                        context.RegisterCodeFix(CodeAction.Create(title: title2, createChangedDocument: c => GenerateExplicitConversion(context.Document, assignmentExpression, true, c), equivalenceKey: title2), diagnostic);
                    }
                    break;
                case ReturnStatementSyntax returnStatement:
                    context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => GenerateExplicitConversion(context.Document, returnStatement, c), equivalenceKey: title), diagnostic);
                    break; 
                case YieldStatementSyntax yieldStatement:
                    context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => GenerateExplicitConversion(context.Document, yieldStatement, c), equivalenceKey: title), diagnostic);
                    break;
            }
        }

        private async Task<Document> GenerateExplicitConversion(Document document, AssignmentExpressionSyntax assignmentExpression, bool reUseInstance, CancellationToken cancellationToken)
        {
            var (mappingEngine, semanticModel) = await CreateMappingEngine(document, cancellationToken).ConfigureAwait(false);
            var sourceType = mappingEngine.GetExpressionTypeInfo(assignmentExpression.Right).GetAnnotatedType();
            var destinationType = mappingEngine.GetExpressionTypeInfo(assignmentExpression.Left).GetAnnotatedType();
            var mappingContext = new MappingContext(assignmentExpression, semanticModel);
            var mappingExpression = await mappingEngine.MapExpression(assignmentExpression.Right.WithoutTrivia(), sourceType, destinationType, mappingContext).ConfigureAwait(false);

            if (reUseInstance && mappingExpression is ObjectCreationExpressionSyntax objectCreation && objectCreation.Initializer is {}  initializer && initializer.Expressions.Count > 0 )
            {
                if (assignmentExpression.Parent is InitializerExpressionSyntax)
                {
                    return await ReplaceNode(document, assignmentExpression, assignmentExpression.WithRight(initializer), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var targetAccessor = assignmentExpression.Left;
                    var newInitExpressions = initializer.Expressions.OfType<AssignmentExpressionSyntax>().Select(expr =>
                    {
                        var left = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetAccessor, (SimpleNameSyntax) expr.Left);
                        return expr.WithLeft(left).AsStatement();
                    }).ToList();
                    return await ReplaceNodes(document, assignmentExpression.Parent, newInitExpressions, cancellationToken).ConfigureAwait(false);
                }
            }

            return await ReplaceNode(document, assignmentExpression, assignmentExpression.WithRight(mappingExpression), cancellationToken).ConfigureAwait(false);
        }

        private static async Task<(MappingEngine, SemanticModel)> CreateMappingEngine(Document document, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var mappingEngine =  new MappingEngine(semanticModel, syntaxGenerator);
            return (mappingEngine, semanticModel);
        }

        private async Task<Document> GenerateExplicitConversion(Document document, ReturnStatementSyntax returnStatement, CancellationToken cancellationToken)
        {
            var (mappingEngine, semanticModel) = await CreateMappingEngine(document, cancellationToken).ConfigureAwait(false);
            var returnExpressionTypeInfo = mappingEngine.GetExpressionTypeInfo(returnStatement.Expression);
            var mappingContext = new MappingContext(returnStatement, semanticModel);
            var mappingExpression = await mappingEngine.MapExpression(returnStatement.Expression!.WithoutTrivia(), returnExpressionTypeInfo.GetAnnotatedType(), returnExpressionTypeInfo.GetAnnotatedTypeForConverted(), mappingContext).ConfigureAwait(false); 
            return await ReplaceNode(document, returnStatement, returnStatement.WithExpression(mappingExpression), cancellationToken).ConfigureAwait(false);
        }

        private async Task<Document> GenerateExplicitConversion(Document document, YieldStatementSyntax yieldStatement, CancellationToken cancellationToken)
        {
            var (mappingEngine, semanticModel) = await CreateMappingEngine(document, cancellationToken).ConfigureAwait(false);
            var returnExpressionTypeInfo = mappingEngine.GetExpressionTypeInfo(yieldStatement.Expression);
            var mappingContext = new MappingContext(yieldStatement, semanticModel);
            var mappingExpression = await mappingEngine.MapExpression(yieldStatement.Expression!.WithoutTrivia(), returnExpressionTypeInfo.GetAnnotatedType(), returnExpressionTypeInfo.GetAnnotatedTypeForConverted(), mappingContext).ConfigureAwait(false); 
            return await ReplaceNode(document, yieldStatement, yieldStatement.WithExpression(mappingExpression), cancellationToken).ConfigureAwait(false);
        }

        private static async Task<Document> ReplaceNode(Document document, SyntaxNode oldNode, SyntaxNode newNode, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = root.ReplaceNode(oldNode, newNode);
            return document.WithSyntaxRoot(newRoot);
        }
        
        private static async Task<Document> ReplaceNodes(Document document, SyntaxNode oldNode, IReadOnlyList<SyntaxNode> newNodes, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = root.ReplaceNode(oldNode, newNodes);
            return document.WithSyntaxRoot(newRoot);
        }

        private SyntaxNode FindStatementToReplace(SyntaxNode node)
        {
            return node switch
            {
                //TODO EqualsValueClauseSyntax - Type1 v = vOfType2
                AssignmentExpressionSyntax assignmentStatement => assignmentStatement,
                ReturnStatementSyntax returnStatement => returnStatement,
                YieldStatementSyntax yieldStatement => yieldStatement,
                _ => node.Parent == null ? null : FindStatementToReplace(node.Parent),
            };
        }
    }
}
