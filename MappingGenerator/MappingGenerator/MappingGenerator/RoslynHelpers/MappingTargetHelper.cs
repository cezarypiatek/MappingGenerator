using System.Collections.Generic;
using System.Linq;
using MappingGenerator.Mappings;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.RoslynHelpers
{
    public static class MappingTargetHelper
    {
        public static IReadOnlyCollection<IObjectField> GetFieldsThaCanBeSetFromConstructor(ITypeSymbol type, MappingContext mappingContext)
        {
            return type.GetObjectFields().Where(x=>x.CanBeSetInConstructor(type, mappingContext)).ToList();
        }

        public static IReadOnlyCollection<IObjectField> GetFieldsThaCanBeSetPublicly(ITypeSymbol type,
            MappingContext mappingContext)
        {
            return type.GetObjectFields().Where(x => x.CanBeSet(type, mappingContext)).ToList();
        }

        public static IReadOnlyCollection<IObjectField> GetFieldsThaCanBeSetPrivately(ITypeSymbol type, MappingContext mappingContext)
        {
            return type.GetObjectFields().Where(x => x.CanBeSet(type, mappingContext)).ToList();
        }
    }
}