using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.RoslynHelpers
{
    public static class MappingTargetHelper
    {
        public static IEnumerable<IObjectField> GetFieldsThaCanBeSetFromConstructor(ITypeSymbol type)
        {
            return GetRegularPropertySymbols(type)
                .Select(x => new ObjectProperty(x))
                .Where(x=>x.CanBeSetInConstructor());
        }

        public static IEnumerable<IObjectField> GetFieldsThaCanBeSetPublicly(ITypeSymbol type, IAssemblySymbol contextAssembly)
        {
            var canSetInternalFields = contextAssembly.IsSameAssemblyOrHasFriendAccessTo(type.ContainingAssembly);
            return GetRegularPropertySymbols(type)
                .Select(x => new ObjectProperty(x))
                .Where(x => x.CanBeSetPublicly(contextAssembly));
        }

        public static IEnumerable<IObjectField> GetFieldsThaCanBeSetPrivately(ITypeSymbol type)
        {
            return GetRegularPropertySymbols(type)
                .Select(x => new ObjectProperty(x))
                .Where(x => x.CanBeSetPrivately(type));
        }

        private static IEnumerable<IPropertySymbol> GetRegularPropertySymbols(ITypeSymbol source)
        {
            return source.GetBaseTypesAndThis().SelectMany(x => x.GetMembers()).OfType<IPropertySymbol>().Where(x => x.IsStatic == false && x.IsIndexer == false);
        }

    }
}