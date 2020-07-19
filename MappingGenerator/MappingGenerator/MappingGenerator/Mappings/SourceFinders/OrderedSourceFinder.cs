using System.Collections.Generic;

namespace MappingGenerator.Mappings.SourceFinders
{
    class OrderedSourceFinder : IMappingSourceFinder
    {
        private readonly IReadOnlyList<IMappingSourceFinder> sourceFinders;

        public OrderedSourceFinder(IReadOnlyList<IMappingSourceFinder> sourceFinders)
        {
            this.sourceFinders = sourceFinders;
        }

        public MappingElement FindMappingSource(string targetName, AnnotatedType targetType, MappingContext mappingContext)
        {
            foreach (var sourceFinder in sourceFinders)
            {
                var mappingElement = sourceFinder.FindMappingSource(targetName, targetType, mappingContext);
                if (mappingElement != null)
                {
                    return mappingElement;
                }
            }

            return null;
        }
    }
}