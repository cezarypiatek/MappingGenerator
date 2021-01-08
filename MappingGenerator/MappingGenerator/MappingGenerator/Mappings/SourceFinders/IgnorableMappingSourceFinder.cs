using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.Mappings.SourceFinders
{
    public class IgnorableMappingSourceFinder:IMappingSourceFinder
    {
        private readonly IMappingSourceFinder wrappedFinder;
        private readonly Func<MappingElement, bool> ignore;

        public IgnorableMappingSourceFinder(IMappingSourceFinder wrappedFinder, Func<MappingElement, bool> ignore)
        {
            this.wrappedFinder = wrappedFinder;
            this.ignore = ignore;
        }

        public async Task<SourceMappingElement> FindMappingSource(string targetName, AnnotatedType targetType, MappingContext mappingContext)
        {
            var mapping =  await wrappedFinder.FindMappingSource(targetName, targetType, mappingContext).ConfigureAwait(false);
            return mapping == null || ignore(mapping) ? null : mapping;
        }

    }
}