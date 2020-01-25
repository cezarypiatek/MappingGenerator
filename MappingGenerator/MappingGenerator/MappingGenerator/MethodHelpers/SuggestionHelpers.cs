using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.MethodHelpers
{
    internal class SuggestionHelpers
    {

        internal static SyntaxToken GetCurrentArgumentListSyntaxToken(SyntaxNode node, int currentPosition)
        {
            var allArgumentLists = node.DescendantNodes(n => n.FullSpan.Contains(currentPosition - 1)).OfType<ArgumentListSyntax>().OrderBy(n => n.FullSpan.Length);
            return allArgumentLists.SelectMany(n => n.ChildTokens()
                .Where(t => t.IsKind(SyntaxKind.OpenParenToken) || t.IsKind(SyntaxKind.CommaToken))
                .Where(t => t.FullSpan.Contains(currentPosition - 1))).FirstOrDefault();
        }
    }
}