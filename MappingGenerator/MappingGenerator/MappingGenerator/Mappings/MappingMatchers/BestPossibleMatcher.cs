using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingMatchers
{
    class BestPossibleMatcher : IMappingMatcher
    {
        private readonly IReadOnlyList<SingleSourceMatcher> matchers;

        public BestPossibleMatcher(IReadOnlyList<IMappingSourceFinder> sourceFinders)
        {
            matchers = sourceFinders.Select(x => new SingleSourceMatcher(x)).ToList();
        }

        public async Task<IReadOnlyList<MappingMatch>> MatchAll(TargetHolder targetHolder,
            SyntaxGenerator syntaxGenerator, MappingContext mappingContext)
        {
            var matchedSets = await GetMatchedSets(targetHolder, syntaxGenerator, mappingContext).ConfigureAwait(false);
            return matchedSets.OrderByDescending(x => x.Count).FirstOrDefault() ?? Array.Empty<MappingMatch>();
        }

        private async Task<IReadOnlyList<IReadOnlyList<MappingMatch>>> GetMatchedSets(TargetHolder targetHolder, SyntaxGenerator syntaxGenerator, MappingContext mappingContext)
        {
            var results = new List<IReadOnlyList<MappingMatch>>();
            foreach (var x in matchers)
            {
                var matches = await x.MatchAll(targetHolder, syntaxGenerator, mappingContext).ConfigureAwait(false);
                results.Add(matches);
                if (matches.Count == targetHolder.ElementsToSet.Count)
                {
                    break;
                }
            }

            return results;
        }
    }
}
