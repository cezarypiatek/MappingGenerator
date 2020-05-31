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

        public IReadOnlyList<MappingMatch> MatchAll(IEnumerable<IPropertySymbol> targets,
            SyntaxGenerator syntaxGenerator, MappingContext mappingContext, SyntaxNode globalTargetAccessor = null)
        {
            return targets.Select(property => new MappingMatch
                {
                    Source = sourceFinder.FindMappingSource(property.Name, property.Type, mappingContext),
                    Target = CreateTargetElement(globalTargetAccessor, property, syntaxGenerator)
                })
                .Where(x => x.Source != null).ToList();
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
}