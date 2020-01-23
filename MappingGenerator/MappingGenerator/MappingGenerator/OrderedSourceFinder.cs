using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace MappingGenerator
{
    class OrderedSourceFinder : IMappingSourceFinder
    {
        private readonly IReadOnlyList<IMappingSourceFinder> sourceFinders;

        public OrderedSourceFinder(IReadOnlyList<IMappingSourceFinder> sourceFinders)
        {
            this.sourceFinders = sourceFinders;
        }

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            foreach (var sourceFinder in sourceFinders)
            {
                var mappingElement = sourceFinder.FindMappingSource(targetName, targetType);
                if (mappingElement != null)
                {
                    return mappingElement;
                }
            }

            return null;
        }
    }
}