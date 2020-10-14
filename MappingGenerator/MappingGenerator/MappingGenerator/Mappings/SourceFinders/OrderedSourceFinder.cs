using System.Collections.Generic;
using System.Threading.Tasks;

namespace MappingGenerator.Mappings.SourceFinders
{
    class OrderedSourceFinder : IMappingSourceFinder
    {
        private readonly IReadOnlyList<IMappingSourceFinder> sourceFinders;

        public OrderedSourceFinder(IReadOnlyList<IMappingSourceFinder> sourceFinders)
        {
            this.sourceFinders = sourceFinders;
        }

        public async Task<MappingElement> FindMappingSource(string targetName, AnnotatedType targetType, MappingContext mappingContext)
        {
            foreach (var sourceFinder in sourceFinders)
            {
                var mappingElement = await sourceFinder.FindMappingSource(targetName, targetType, mappingContext).ConfigureAwait(false);
                if (mappingElement != null)
                {
                    return mappingElement;
                }
            }

            return null;
        }
    }
}