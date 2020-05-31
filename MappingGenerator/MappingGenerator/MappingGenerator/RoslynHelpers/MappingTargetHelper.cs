using System.Collections.Generic;
using System.Linq;
using MappingGenerator.Mappings;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.RoslynHelpers
{
    public static class MappingTargetHelper
    {
        public static IEnumerable<IObjectField> GetFieldsThaCanBeSetFromConstructor(ITypeSymbol type, MappingContext mappingContext)
        {
            return type.GetObjectFields().Where(x=>x.CanBeSetInConstructor(type, mappingContext));
        }

        public static IEnumerable<IObjectField> GetFieldsThaCanBeSetPublicly(ITypeSymbol type, IAssemblySymbol contextAssembly, MappingContext mappingContext)
        {
            return type.GetObjectFields().Where(x => x.CanBeSet(type, mappingContext));
        }

        public static IEnumerable<IObjectField> GetFieldsThaCanBeSetPrivately(ITypeSymbol type, MappingContext mappingContext)
        {
            return type.GetObjectFields().Where(x => x.CanBeSet(type, mappingContext));
        }
    }
}