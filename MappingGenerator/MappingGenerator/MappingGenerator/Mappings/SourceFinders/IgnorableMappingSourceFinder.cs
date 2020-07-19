using System;
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

        public MappingElement FindMappingSource(string targetName, AnnotatedType targetType, MappingContext mappingContext)
        {
            var mapping = wrappedFinder.FindMappingSource(targetName, targetType, mappingContext);
            return mapping == null || ignore(mapping) ? null : mapping;
        }

    }
}