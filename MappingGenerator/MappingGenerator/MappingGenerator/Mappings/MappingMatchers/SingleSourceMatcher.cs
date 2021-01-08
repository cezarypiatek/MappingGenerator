using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<IReadOnlyList<MappingMatch>> MatchAll(IReadOnlyCollection<IObjectField> targets, SyntaxGenerator syntaxGenerator, MappingContext mappingContext, SyntaxNode globalTargetAccessor = null)
        {
            var results = new List<MappingMatch>(targets.Count);
            foreach (var target in targets)
            {
                results.Add(new MappingMatch
                {
                    Source = await sourceFinder.FindMappingSource(target.Name, target.Type, mappingContext).ConfigureAwait(false),
                    Target = CreateTargetElement(globalTargetAccessor, target, mappingContext)
                });
            }

            return results.Where(x => x.Source != null).ToList();
        }

        private TargetMappingElement CreateTargetElement(SyntaxNode globalTargetAccessor, IObjectField target, MappingContext mappingContext)
        {
            return new TargetMappingElement
            {
                Expression = (ExpressionSyntax)CreateAccessPropertyExpression(globalTargetAccessor, target),
                ExpressionType = target.Type,
                OnlyIndirectInit = target.CanBeSetOnlyIndirectly(,mappingContext)
            };
        }

        private static SyntaxNode CreateAccessPropertyExpression(SyntaxNode globalTargetAccessor, IObjectField property)
        {
            if (globalTargetAccessor == null)
            {
                return SyntaxFactory.IdentifierName(property.Name);
            }
            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, (ExpressionSyntax)globalTargetAccessor, SyntaxFactory.IdentifierName(property.Name));
        }
    }
}
