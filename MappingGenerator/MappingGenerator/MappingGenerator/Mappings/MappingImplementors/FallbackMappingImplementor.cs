using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<IReadOnlyList<SyntaxNode>> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator,
            SemanticModel semanticModel, MappingContext mappingContext)
        {
            foreach (var implementor in implementors)
            {
                var result = await implementor.GenerateImplementation(methodSymbol, generator, semanticModel, mappingContext).ConfigureAwait(false);
                if (result.Count > 0)
                {
                    return result;
                }
            }

            return Array.Empty<SyntaxNode>();
        }
    }
}