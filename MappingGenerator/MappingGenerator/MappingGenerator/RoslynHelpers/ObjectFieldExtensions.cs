using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.RoslynHelpers
{
    public static class ObjectFieldExtensions
    {
        public static IEnumerable<IObjectField> GetObjectFields(this ITypeSymbol type)
        {
            return GetRegularPropertySymbols(type)
                .Select(x => new ObjectProperty(x));
        }

        private static IEnumerable<IPropertySymbol> GetRegularPropertySymbols(this ITypeSymbol source)
        {
            return source.GetBaseTypesAndThis().SelectMany(x => x.GetMembers()).OfType<IPropertySymbol>().Where(x => x.IsStatic == false && x.IsIndexer == false);
        }
    }
}