using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.RoslynHelpers
{
    internal static class SymbolHelper
    {
        public static bool IsConstructor(IMethodSymbol methodSymbol)
        {
            return methodSymbol.MethodKind == MethodKind.Constructor;
        }

        public static IEnumerable<ITypeSymbol> UnwrapGeneric(this ITypeSymbol typeSymbol)
        {
            if (typeSymbol.TypeKind == TypeKind.TypeParameter && typeSymbol is ITypeParameterSymbol namedType && namedType.Kind != SymbolKind.ErrorType)
            {
                return namedType.ConstraintTypes;
            }
            return new []{typeSymbol};
        }

        public static bool IsDeclaredOutsideTheSourcecode(IPropertySymbol property)
        {
            return property.DeclaringSyntaxReferences.Length == 0;
        }

        public static bool IsNullable(ITypeSymbol type, out ITypeSymbol underlyingType)
        {
            if (IsNullable(type))
            {
                underlyingType = GetUnderlyingNullableType(type);
                return true;
            }

            underlyingType = null;
            return false;
        }
        public static bool IsNullable(ITypeSymbol type) => type.TypeKind == TypeKind.Struct && type.Name == "Nullable";

        public static ITypeSymbol GetUnderlyingNullableType(ITypeSymbol type)
        {
            return ((INamedTypeSymbol) type).TypeArguments.First();
        }
    }
}