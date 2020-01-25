using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingImplementors
{
    class FallbackMappingImplementor : IMappingMethodImplementor
    {
        private readonly IMappingMethodImplementor[] implementors;

        public FallbackMappingImplementor(params IMappingMethodImplementor[] implementors)
        {
            this.implementors = implementors;
        }

        public bool CanImplement(IMethodSymbol methodSymbol)
        {
            return implementors.Any(x => x.CanImplement(methodSymbol));
        }

        public IEnumerable<SyntaxNode> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            foreach (var implementor in implementors)
            {
                var result = implementor.GenerateImplementation(methodSymbol, generator, semanticModel).ToList();
                if (result.Count > 0)
                {
                    return result;
                }
            }

            return Enumerable.Empty<SyntaxNode>();
        }
    }
}