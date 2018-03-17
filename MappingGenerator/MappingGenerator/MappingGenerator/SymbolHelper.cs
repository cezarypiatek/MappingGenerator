using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator
{
    internal static class SymbolHelper
    {
        public static bool IsUpdateParameterFunction(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 2 && methodSymbol.ReturnsVoid;
        }

        public static bool IsUpdateThisObjectFunction(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 && methodSymbol.ReturnsVoid;
        }
        
        public static bool IsMultiParameterUpdateThisObjectFunction(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length > 1 && methodSymbol.ReturnsVoid;
        }

        public static bool IsPureMappingFunction(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 && methodSymbol.ReturnsVoid == false;
        }

        public static bool IsMappingConstructor(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 && methodSymbol.MethodKind == MethodKind.Constructor;
        }  
        
        public static bool IsMultiParameterMappingConstructor(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length > 1 && methodSymbol.MethodKind == MethodKind.Constructor;
        }

        public static IEnumerable<ITypeSymbol> UnwrapGeneric(this ITypeSymbol typeSymbol)
        {
            if (typeSymbol.TypeKind == TypeKind.TypeParameter && typeSymbol is ITypeParameterSymbol namedType && namedType.Kind != SymbolKind.ErrorType)
            {
                return namedType.ConstraintTypes;
            }
            return new []{typeSymbol};
        }

        public static bool IsReadonlyProperty(this IPropertySymbol property)
        {
            var propertyDeclaration = property.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
            if (propertyDeclaration == null || propertyDeclaration.AccessorList.Accessors.Count > 1)
            {
                return false;
            }
            return propertyDeclaration.AccessorList.Accessors.SingleOrDefault(IsAutoGetter) != null;
            
        }

        private static bool IsSetter(AccessorDeclarationSyntax x)
        {
            return x.IsKind(SyntaxKind.SetAccessorDeclaration);
        }

        private static bool IsAutoGetter(AccessorDeclarationSyntax x)
        {
            return x.IsKind(SyntaxKind.GetAccessorDeclaration) && x.Body ==null && x.ExpressionBody == null;
        }
    }
}