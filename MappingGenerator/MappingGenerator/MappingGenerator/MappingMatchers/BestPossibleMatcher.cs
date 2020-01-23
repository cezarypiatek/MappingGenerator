using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.MappingMatchers
{
    class BestPossibleMatcher : IMappingMatcher
    {
        private readonly IReadOnlyList<SingleSourceMatcher> matchers;

        public BestPossibleMatcher(IReadOnlyList<IMappingSourceFinder> sourceFinders)
        {
            matchers = sourceFinders.Select(x => new SingleSourceMatcher(x)).ToList();
        }

        public IReadOnlyList<MappingMatch> MatchAll(IEnumerable<IPropertySymbol> targets, SyntaxGenerator syntaxGenerator, SyntaxNode globalTargetAccessor = null)
        {
            return matchers.Select(x => x.MatchAll(targets, syntaxGenerator, globalTargetAccessor))
                .OrderByDescending(x => x.Count).FirstOrDefault() ?? Array.Empty<MappingMatch>();
        }
    }
}