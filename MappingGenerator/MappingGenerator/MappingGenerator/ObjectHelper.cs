using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MappingGenerator
{
    public class ObjectHelper
    {
        private static bool IsPublicGetMethod(ISymbol x)
        {
            return x is IMethodSymbol mSymbol
                   && mSymbol.ReturnsVoid == false
                   && mSymbol.Parameters.Length == 0
                   && x.DeclaredAccessibility == Accessibility.Public
                   && x.IsImplicitlyDeclared == false
                   && mSymbol.MethodKind == MethodKind.Ordinary
                ;
        }

        private static bool IsPublicPropertySymbol(ISymbol x)
        {
            if (x.Kind != SymbolKind.Property)
            {
                return false;
            }

            if (x is IPropertySymbol mSymbol)
            {
                if (mSymbol.IsStatic || mSymbol.IsIndexer || mSymbol.DeclaredAccessibility != Accessibility.Public)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public static IEnumerable<IPropertySymbol> GetUnwrappingProperties(ITypeSymbol wrapperType, ITypeSymbol wrappedType)
        {
            return GetPublicPropertySymbols(wrapperType).Where(x => x.GetMethod.DeclaredAccessibility == Accessibility.Public && x.Type == wrappedType);
        }

        public static IEnumerable<IMethodSymbol> GetUnwrappingMethods(ITypeSymbol wrapperType, ITypeSymbol wrappedType)
        {
            return GetPublicGetMethods(wrapperType).Where(x => x.DeclaredAccessibility == Accessibility.Public && x.ReturnType == wrappedType);
        }

        public static IEnumerable<IPropertySymbol> GetPublicPropertySymbols(ITypeSymbol source)
        {
            return GetBaseTypesAndThis(source).SelectMany(x=> x.GetMembers()).Where(IsPublicPropertySymbol).OfType<IPropertySymbol>();
        }

        public static IEnumerable<IMethodSymbol> GetPublicGetMethods(ITypeSymbol source)
        {
            return GetBaseTypesAndThis(source).SelectMany(x=> x.GetMembers()).Where(IsPublicGetMethod).OfType<IMethodSymbol>();
        }

        private  static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(ITypeSymbol type)
        {
            foreach (var unwrapped in type.UnwrapGeneric())
            {
                var current = unwrapped;
                while (current != null && IsSystemObject(current) == false)
                {
                    yield return current;
                    current = current.BaseType;
                }    
            }
        }

        private static bool IsSystemObject(ITypeSymbol current)
        {
            return current.Name == "Object" && current.ContainingNamespace.Name =="System";
        }

        private static string[] SimpleTypes = new[] {"String", "Decimal"};

        public static bool IsSimpleType(ITypeSymbol type)
        {
            return type.IsValueType || SimpleTypes.Contains(type.Name);
        }
    }
}