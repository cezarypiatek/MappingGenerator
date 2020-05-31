using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingMatchers
{
    public interface IMappingMatcher
    {
        IReadOnlyList<MappingMatch> MatchAll(IEnumerable<IPropertySymbol> targets, SyntaxGenerator syntaxGenerator,
            MappingContext mappingContext, SyntaxNode globalTargetAccessor = null);
    }
}