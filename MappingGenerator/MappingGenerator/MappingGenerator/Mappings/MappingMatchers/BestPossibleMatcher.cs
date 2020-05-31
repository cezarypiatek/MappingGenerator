using System;
using System.Collections.Generic;
using System.Linq;
using MappingGenerator.Mappings.SourceFinders;
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

        public IReadOnlyList<MappingMatch> MatchAll(IEnumerable<IPropertySymbol> targets,
            SyntaxGenerator syntaxGenerator, MappingContext mappingContext, SyntaxNode globalTargetAccessor = null)
        {
            return matchers.Select(x => x.MatchAll(targets, syntaxGenerator, mappingContext, globalTargetAccessor))
                .OrderByDescending(x => x.Count).FirstOrDefault() ?? Array.Empty<MappingMatch>();
        }
    }
}