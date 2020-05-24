using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingMatchers
{
    public interface IMappingMatcher
    {
        (IReadOnlyList<MappingMatch> matched, IReadOnlyList<IPropertySymbol> unmatched) MatchAll(
            IEnumerable<IPropertySymbol> targets, SyntaxGenerator syntaxGenerator,
            SyntaxNode globalTargetAccessor = null);
    }
}