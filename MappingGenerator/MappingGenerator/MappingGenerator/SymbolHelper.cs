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
            if (IsConstructor(methodSymbol))
            {
                return false;
            }
            return methodSymbol.Parameters.Length == 2 && methodSymbol.ReturnsVoid;
        }

        private static bool IsConstructor(IMethodSymbol methodSymbol)
        {
            return methodSymbol.MethodKind == MethodKind.Constructor;
        }

        public static bool IsUpdateThisObjectFunction(IMethodSymbol methodSymbol)
        {
            if (IsConstructor(methodSymbol))
            {
                return false;
            }
            return methodSymbol.Parameters.Length == 1 && methodSymbol.ReturnsVoid;
        }
        
        public static bool IsMultiParameterUpdateThisObjectFunction(IMethodSymbol methodSymbol)
        {
            if (IsConstructor(methodSymbol))
            {
                return false;
            }
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

        public static bool CanBeSetPrivately(this IPropertySymbol property)
        {
            var propertyDeclaration = property.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
            if (propertyDeclaration?.AccessorList == null)
            {
                return false;
            }
            return HasPrivateSetter(propertyDeclaration) || HasPublicSetter(propertyDeclaration, isInternalAccessible:true);
        } 
        
        public static bool CanBeSetPublicly(this IPropertySymbol property, bool isInternalAccessible)
        {
            var propertyDeclaration = property.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
            if (propertyDeclaration?.AccessorList == null)
            {
                return false;
            }

            return HasPublicSetter(propertyDeclaration, isInternalAccessible);
        }

        public static bool CanBeSetOnlyFromConstructor(this IPropertySymbol property)
        {
            var propertyDeclaration = property.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
            if (propertyDeclaration?.AccessorList == null)
            {
                return false;
            }

            if (HasPrivateSetter(propertyDeclaration))
            {
                return false;
            }

            return propertyDeclaration.AccessorList.Accessors.Count == 1 && propertyDeclaration.AccessorList.Accessors.SingleOrDefault(IsAutoGetter) != null;
        }

        private static bool HasPrivateSetter(PropertyDeclarationSyntax propertyDeclaration)
        {
            return propertyDeclaration.AccessorList.Accessors.Any(x =>x.Keyword.Kind() == SyntaxKind.SetKeyword && x.Modifiers.Any(m => m.Kind() == SyntaxKind.PrivateKeyword));
        } 
        
        private static bool HasPublicSetter(PropertyDeclarationSyntax propertyDeclaration, bool isInternalAccessible)
        {
            return propertyDeclaration.AccessorList.Accessors.Any(x =>
            {
                if (x.Keyword.Kind() == SyntaxKind.SetKeyword)
                {
                    return x.Modifiers.Count == 0 || x.Modifiers.Any(m => AllowsForPublic(m, isInternalAccessible));
                }
                return false;
            });
        }

        private static bool AllowsForPublic(SyntaxToken accessor, bool isInternalAccessible)
        {
            switch (accessor.Kind())
            {
                case SyntaxKind.PrivateKeyword:
                case SyntaxKind.ProtectedKeyword:
                    return false;
                case SyntaxKind.InternalKeyword when isInternalAccessible:
                    return true;
                case SyntaxKind.PublicKeyword:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAutoGetter(AccessorDeclarationSyntax x)
        {
            return x.IsKind(SyntaxKind.GetAccessorDeclaration) && x.Body ==null && x.ExpressionBody == null;
        }
    }
}