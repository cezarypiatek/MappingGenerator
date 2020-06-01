using System;
using System.Collections.Generic;
using System.Linq;
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

        public IReadOnlyList<MappingMatch> MatchAll(IReadOnlyCollection<IObjectField> targets,
            SyntaxGenerator syntaxGenerator, MappingContext mappingContext, SyntaxNode globalTargetAccessor = null)
        {
            return GetMatchedSets(targets, syntaxGenerator, mappingContext, globalTargetAccessor)
                .OrderByDescending(x => x.Count).FirstOrDefault() ?? Array.Empty<MappingMatch>();
        }

        private IEnumerable<IReadOnlyList<MappingMatch>> GetMatchedSets(IReadOnlyCollection<IObjectField> targets, SyntaxGenerator syntaxGenerator, MappingContext mappingContext, SyntaxNode globalTargetAccessor)
        {
            foreach (var x in matchers)
            {
                var matches = x.MatchAll(targets, syntaxGenerator, mappingContext, globalTargetAccessor);
                yield return matches;
                if (matches.Count == targets.Count)
                {
                    yield break;
                }
            }
        }
    }
}
