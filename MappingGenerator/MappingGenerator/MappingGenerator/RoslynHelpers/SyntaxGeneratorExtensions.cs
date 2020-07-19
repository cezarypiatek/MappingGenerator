using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.RoslynHelpers
{
    public static class SyntaxGeneratorExtensions
    {
        public static SyntaxNode CompleteAssignmentStatement(this SyntaxGenerator generator, SyntaxNode left, SyntaxNode right)
        {
            var assignmentExpression = generator.AssignmentStatement(left, right);
            return generator.ExpressionStatement(assignmentExpression);
        }
        
        public static SyntaxNode ContextualReturnStatement(this SyntaxGenerator generator, SyntaxNode nodeToReturn, bool generatorContext)
        {
            if (generatorContext)
            {
                return SyntaxFactory.YieldStatement(SyntaxKind.YieldReturnStatement, nodeToReturn as ExpressionSyntax);
            }
            return generator.ReturnStatement(nodeToReturn);
        }

        public static async Task<Document> ReplaceNodes(this Document document, SyntaxNode oldNode, SyntaxNode newNode, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root!.ReplaceNode(oldNode, newNode);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}