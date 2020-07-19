using System.Collections.Generic;
using System.Linq;
using MappingGenerator.Mappings;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.RoslynHelpers
{
    public static class ObjectHelper
    {
        public static IEnumerable<IMethodSymbol> GetWithGetPrefixMethods(ITypeSymbol source)
        {
            var allMembers = source.GetAllMembers();
            return GetWithGetPrefixMethods(allMembers);
        }

        public static IEnumerable<IMethodSymbol> GetWithGetPrefixMethods(IEnumerable<ISymbol> allMembers)
        {
            return allMembers.OfType<IMethodSymbol>().Where(mSymbol =>
                mSymbol.ReturnsVoid == false
                && mSymbol.IsStatic == false
                && mSymbol.IsImplicitlyDeclared == false
                && mSymbol.Parameters.Length == 0
                && mSymbol.MethodKind == MethodKind.Ordinary);
        }

        public static IReadOnlyCollection<ISymbol> GetAllMembers(this ITypeSymbol type)
        {
            return type.GetBaseTypesAndThis().SelectMany(x => x.GetMembers()).ToList();
        }

        public static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(this ITypeSymbol type)
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

        public static bool IsSystemObject(ITypeSymbol current)
        {
            return current.Name == "Object" && current.ContainingNamespace.Name =="System";
        }

        public static bool IsReadonlyCollection(ITypeSymbol current)
        {
            return current.Name == "ReadOnlyCollection";
        }

        public static bool IsSimpleType(ITypeSymbol type)
        {
            
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_Byte:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Char:
                case SpecialType.System_String:
                case SpecialType.System_Object:
                case SpecialType.System_Decimal:
                case SpecialType.System_DateTime:
                case SpecialType.System_Enum:
                    return true;
            }


            switch (type.TypeKind)
            {
                case TypeKind.Enum:
                    return true;
            }

            if (type.ToDisplayString() == "System.Guid")
            {
                return true;
            }


            return false;
        }

        public static bool HasInterface(ITypeSymbol xt, string interfaceName)
        {
            return xt.OriginalDefinition.AllInterfaces.Any(x => x.ToDisplayString() == interfaceName);
        }

    }

    public interface IObjectField
    {
        string Name { get; }
        AnnotatedType Type { get; }


        bool CanBeSet(ITypeSymbol via, MappingContext mappingContext);
        bool CanBeSetInConstructor(ITypeSymbol via, MappingContext mappingContext);

        bool CanBeGet(ITypeSymbol via, MappingContext mappingContext);
    }
}