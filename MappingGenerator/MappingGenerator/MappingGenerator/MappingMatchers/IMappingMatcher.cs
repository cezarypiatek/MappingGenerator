using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.MappingMatchers
{
    public interface IMappingMatcher
    {
        IReadOnlyList<MappingMatch> MatchAll(IEnumerable<IPropertySymbol> targets, SyntaxGenerator syntaxGenerator, SyntaxNode globalTargetAccessor = null);
    }
}