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

        public async Task<IReadOnlyList<MappingMatch>> MatchAll(IReadOnlyCollection<IObjectField> targets,
            SyntaxGenerator syntaxGenerator, MappingContext mappingContext, SyntaxNode globalTargetAccessor = null)
        {
            var matchedSets = await GetMatchedSets(targets, syntaxGenerator, mappingContext, globalTargetAccessor).ConfigureAwait(false);
            return matchedSets.OrderByDescending(x => x.Count).FirstOrDefault() ?? Array.Empty<MappingMatch>();
        }

        private async Task<IReadOnlyList<IReadOnlyList<MappingMatch>>> GetMatchedSets(IReadOnlyCollection<IObjectField> targets, SyntaxGenerator syntaxGenerator, MappingContext mappingContext, SyntaxNode globalTargetAccessor)
        {
            var results = new List<IReadOnlyList<MappingMatch>>();
            foreach (var x in matchers)
            {
                var matches = await x.MatchAll(targets, syntaxGenerator, mappingContext, globalTargetAccessor).ConfigureAwait(false);
                results.Add(matches);
                if (matches.Count == targets.Count)
                {
                    break;
                }
            }

            return results;
        }
    }
}
