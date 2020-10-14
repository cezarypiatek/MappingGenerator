using System.Collections.Generic;
using System.Threading.Tasks;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingMatchers
{
    public interface IMappingMatcher
    {
        Task<IReadOnlyList<MappingMatch>> MatchAll(IReadOnlyCollection<IObjectField> targets, SyntaxGenerator syntaxGenerator,
            MappingContext mappingContext, SyntaxNode globalTargetAccessor = null);
    }
}
