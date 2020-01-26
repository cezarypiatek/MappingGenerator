using System.Collections.Generic;
using System.Linq;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.RoslynHelpers;
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

        public IReadOnlyList<MappingMatch> MatchAll(IEnumerable<IObjectField> targets, SyntaxGenerator syntaxGenerator,  SyntaxNode globalTargetAccessor = null)
        {
            return targets.Select(property => new MappingMatch
                {
                    Source = sourceFinder.FindMappingSource(property.Name, property.Type),
                    Target = CreateTargetElement(globalTargetAccessor, property, syntaxGenerator)
                })
                .Where(x => x.Source != null).ToList();
        }

        private MappingElement CreateTargetElement(SyntaxNode globalTargetAccessor, IObjectField property,
            SyntaxGenerator syntaxGenerator)
        {
            return new MappingElement()
            {
                Expression = (ExpressionSyntax)CreateAccessPropertyExpression(globalTargetAccessor, property, syntaxGenerator),
                ExpressionType = property.Type
            };
        }

        private static SyntaxNode CreateAccessPropertyExpression(SyntaxNode globalTargetAccessor, IObjectField property, SyntaxGenerator generator)
        {
            if (globalTargetAccessor == null)
            {
                return SyntaxFactory.IdentifierName(property.Name);
            }
            return generator.MemberAccessExpression(globalTargetAccessor, property.Name);
        }
    }
}