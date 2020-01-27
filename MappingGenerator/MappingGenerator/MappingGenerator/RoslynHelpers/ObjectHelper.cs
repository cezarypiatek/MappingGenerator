using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.RoslynHelpers
{
    public static class ObjectHelper
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

        private static bool IsPublicPropertySymbol(IPropertySymbol x)
        {
            if (x.IsStatic || x.IsIndexer || x.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
            return true;
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
            return GetBaseTypesAndThis(source).SelectMany(x=> x.GetMembers()).OfType<IPropertySymbol>().Where(IsPublicPropertySymbol);
        }
        public static IEnumerable<IMethodSymbol> GetPublicGetMethods(ITypeSymbol source)
        {
            return GetBaseTypesAndThis(source).SelectMany(x=> x.GetMembers()).Where(IsPublicGetMethod).OfType<IMethodSymbol>();
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

            return false;
        }

        public static bool HasInterface(ITypeSymbol xt, string interfaceName)
        {
            return xt.OriginalDefinition.AllInterfaces.Any(x => x.ToDisplayString() == interfaceName);
        }

        public static bool IsSameAssemblyOrHasFriendAccessTo(this IAssemblySymbol assembly, IAssemblySymbol toAssembly)
        {
            var areEquals = assembly.Equals(toAssembly);
            if (areEquals  == false  &&  toAssembly == null)
            {
                return false;
            }

            return
                areEquals ||
                (assembly.IsInteractive && toAssembly.IsInteractive) ||
                toAssembly.GivesAccessTo(assembly);
        }

        public static IAssemblySymbol FindContextAssembly(this SemanticModel semanticModel, SyntaxNode node )
        {
            var type = node.FindNearestContainer<ClassDeclarationSyntax, StructDeclarationSyntax>();
            var symbol = semanticModel.GetDeclaredSymbol(type);
            return symbol.ContainingAssembly;
        }
    }

    public interface IObjectField
    {
        string Name { get; }
        ITypeSymbol Type { get; }
        ISymbol UnderlyingSymbol { get; }

        bool CanBeSetPublicly(IAssemblySymbol contextAssembly);
        bool CanBeSetPrivately(ITypeSymbol fromType);
        bool CanBeSetInConstructor();
    }
}