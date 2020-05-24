using System.Collections.Generic;
using System.Linq;
using MappingGenerator.Mappings.SourceFinders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingMatchers
{
    class SingleSourceMatcher : IMappingMatcher
    {
        private readonly IMappingSourceFinder sourceFinder;

        public SingleSourceMatcher(IMappingSourceFinder sourceFinder)
        {
            this.sourceFinder = sourceFinder;
        }

        public (IReadOnlyList<MappingMatch> matched, IReadOnlyList<IPropertySymbol> unmatched) MatchAll(IEnumerable<IPropertySymbol> targets, SyntaxGenerator syntaxGenerator,  SyntaxNode globalTargetAccessor = null)
        {
            var matched = new List<MappingMatch>();
            var unmatchedTargets = new List<IPropertySymbol>();
            foreach (var target in targets)
            {
                if (sourceFinder.FindMappingSource(target.Name, target.Type) is { } matchingSource)
                {
                    matched.Add(new MappingMatch()
                    {
                      Source  = matchingSource,
                      Target = CreateTargetElement(globalTargetAccessor, target, syntaxGenerator),
                    });
                }

                else
                {
                    unmatchedTargets.Add(target);
                }
            }

            return (matched, unmatchedTargets);
        }

        private MappingElement CreateTargetElement(SyntaxNode globalTargetAccessor, IPropertySymbol property,
            SyntaxGenerator syntaxGenerator)
        {
            return new MappingElement()
            {
                Expression = (ExpressionSyntax)CreateAccessPropertyExpression(globalTargetAccessor, property, syntaxGenerator),
                ExpressionType = property.Type
            };
        }

        private static SyntaxNode CreateAccessPropertyExpression(SyntaxNode globalTargetAccessor, IPropertySymbol property, SyntaxGenerator generator)
        {
            if (globalTargetAccessor == null)
            {
                return SyntaxFactory.IdentifierName(property.Name);
            }
            return generator.MemberAccessExpression(globalTargetAccessor, property.Name);
        }
    }

    class MappingMatchSummary
    {
        public IReadOnlyList<MappingMatch> Matched { get; set; }
        public IReadOnlyList<IPropertySymbol> UnmatchedTargets { get; set; }

    }
}