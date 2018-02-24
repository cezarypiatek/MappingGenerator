using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
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
    }
}