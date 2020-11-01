using System.Collections.Generic;
using System.Linq;
using MappingGenerator.Mappings;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.RoslynHelpers
{
    public class MappingTargetHelper
    {
        private static readonly NameEqualityComparer nameEqualityComparer = new NameEqualityComparer();
        public IReadOnlyCollection<IObjectField> GetFieldsThaCanBeSetFromConstructor(ITypeSymbol type, MappingContext mappingContext)
        {
            return GetObjectFields(type).Where(x=>x.CanBeSetInConstructor(type, mappingContext)).Distinct(nameEqualityComparer).ToList();
        }

        public  IReadOnlyCollection<IObjectField> GetFieldsThaCanBeSetPublicly(ITypeSymbol type,
            MappingContext mappingContext)
        {
            return GetObjectFields(type).Where(x => x.CanBeSet(type, mappingContext)).Distinct(nameEqualityComparer).ToList();
        }

        public  IReadOnlyCollection<IObjectField> GetFieldsThaCanBeSetPrivately(ITypeSymbol type, MappingContext mappingContext)
        {
            return GetObjectFields(type).Where(x => x.CanBeSet(type, mappingContext)).Distinct(nameEqualityComparer).ToList(); 
        }

        private readonly Dictionary<ITypeSymbol, IReadOnlyList<IObjectField>> fieldsCache = new Dictionary<ITypeSymbol, IReadOnlyList<IObjectField>>();
        private  IReadOnlyList<IObjectField> GetObjectFields(ITypeSymbol type)
        {
            if (fieldsCache.TryGetValue(type, out var fields))
            {
                return fields;
            }

            var freshFields = type.GetObjectFields().ToList();
            fieldsCache[type] = freshFields;
            return freshFields;
        }


        class NameEqualityComparer : IEqualityComparer<IObjectField>
        {
            public bool Equals(IObjectField x, IObjectField y) => x?.Name == y?.Name;

            public int GetHashCode(IObjectField obj) => obj.Name.GetHashCode();
        }

    }
}