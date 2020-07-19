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

        public IReadOnlyList<MappingMatch> MatchAll(IReadOnlyCollection<IObjectField> targets,
            SyntaxGenerator syntaxGenerator, MappingContext mappingContext, SyntaxNode globalTargetAccessor = null)
        {
            return targets.Select(target => new MappingMatch
                {
                    Source = sourceFinder.FindMappingSource(target.Name, target.Type, mappingContext),
                    Target = CreateTargetElement(globalTargetAccessor, target, syntaxGenerator)
                })
                .Where(x => x.Source != null).ToList();
        }

        private MappingElement CreateTargetElement(SyntaxNode globalTargetAccessor, IObjectField target, SyntaxGenerator syntaxGenerator)
        {
            return new MappingElement
            {
                Expression = (ExpressionSyntax)CreateAccessPropertyExpression(globalTargetAccessor, target, syntaxGenerator),
                ExpressionType = target.Type
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
