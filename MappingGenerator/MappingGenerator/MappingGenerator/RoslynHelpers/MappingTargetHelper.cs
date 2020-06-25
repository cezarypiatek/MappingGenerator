using System.Collections.Generic;
using System.Linq;
using MappingGenerator.Mappings;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.RoslynHelpers
{
    public static class MappingTargetHelper
    {
        private static readonly NameEqualityComparer nameEqualityComparer = new NameEqualityComparer();
        public static IReadOnlyCollection<IObjectField> GetFieldsThaCanBeSetFromConstructor(ITypeSymbol type, MappingContext mappingContext)
        {
            return type.GetObjectFields().Where(x=>x.CanBeSetInConstructor(type, mappingContext)).Distinct(nameEqualityComparer).ToList();
        }

        public static IReadOnlyCollection<IObjectField> GetFieldsThaCanBeSetPublicly(ITypeSymbol type,
            MappingContext mappingContext)
        {
            return type.GetObjectFields().Where(x => x.CanBeSet(type, mappingContext)).Distinct(nameEqualityComparer).ToList();
        }

        public static IReadOnlyCollection<IObjectField> GetFieldsThaCanBeSetPrivately(ITypeSymbol type, MappingContext mappingContext)
        {
            return type.GetObjectFields().Where(x => x.CanBeSet(type, mappingContext)).Distinct(nameEqualityComparer).ToList(); 
        }

        class NameEqualityComparer : IEqualityComparer<IObjectField>
        {
            public bool Equals(IObjectField x, IObjectField y) => x?.Name == y?.Name;

            public int GetHashCode(IObjectField obj) => obj.Name.GetHashCode();
        }

    }
}