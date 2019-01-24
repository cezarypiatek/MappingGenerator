using System;
using Microsoft.CodeAnalysis;

namespace MappingGenerator
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

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            var mapping = wrappedFinder.FindMappingSource(targetName, targetType);
            return ignore(mapping) ? null : mapping;
        }

    }
}