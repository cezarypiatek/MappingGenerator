using System.Collections.Generic;
using System.Threading.Tasks;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingMatchers
{
    public interface IMappingMatcher
    {
        Task<IReadOnlyList<MappingMatch>> MatchAll(TargetHolder targetHolder, SyntaxGenerator syntaxGenerator, MappingContext mappingContext);
    }
}
