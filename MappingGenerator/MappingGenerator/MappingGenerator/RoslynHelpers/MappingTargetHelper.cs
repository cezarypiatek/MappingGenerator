using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.RoslynHelpers
{
    public static class MappingTargetHelper
    {
        public static IEnumerable<IObjectField> GetFieldsThaCanBeSetFromConstructor(ITypeSymbol type)
        {
            return type.GetObjectFields().Where(x=>x.CanBeSetInConstructor(type));
        }

        public static IEnumerable<IObjectField> GetFieldsThaCanBeSetPublicly(ITypeSymbol type, IAssemblySymbol contextAssembly)
        {
            return type.GetObjectFields().Where(x => x.CanBeSetPublicly(contextAssembly));
        }

        public static IEnumerable<IObjectField> GetFieldsThaCanBeSetPrivately(ITypeSymbol type)
        {
            return type.GetObjectFields().Where(x => x.CanBeSetPrivately(type));
        }
    }
}