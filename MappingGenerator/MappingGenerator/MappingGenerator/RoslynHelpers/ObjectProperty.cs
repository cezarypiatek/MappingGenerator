using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.RoslynHelpers
{
    public class ObjectProperty : IObjectField
    {
        private readonly IPropertySymbol property;

        public ObjectProperty(IPropertySymbol property)
        {
            this.property = property;
        }

        public string Name => property.Name;

        public ITypeSymbol Type => property.Type;

        public ISymbol UnderlyingSymbol => property;
        public bool CanBeSetPublicly(IAssemblySymbol contextAssembly)
        {
            if(property.SetMethod == null)
            {
                return false;
            }

            var canSetInternalFields = contextAssembly.IsSameAssemblyOrHasFriendAccessTo(property.ContainingAssembly);
            if(property.DeclaredAccessibility != Accessibility.Public)
            {
                if (property.DeclaredAccessibility != Accessibility.Internal || canSetInternalFields == false)
                {
                    return false;
                }
            }

            if (property.SetMethod.DeclaredAccessibility != Accessibility.Public)
            {
                if (property.SetMethod.DeclaredAccessibility != Accessibility.Internal || canSetInternalFields == false)
                {
                    return false;
                }
            }

            return true;
        }

        public bool CanBeSetPrivately(ITypeSymbol fromType)
        {
            if (property.SetMethod == null)
            {
                return false;
            }

            if (this.CanBeSetPublicly(fromType.ContainingAssembly))
            {
                return true;
            }

            if (property.SetMethod.DeclaredAccessibility == Accessibility.Protected)
            {
                return true;
            }

            return property.SetMethod.DeclaredAccessibility == Accessibility.Private && property.ContainingType.Equals(fromType);
        }

        public bool CanBeSetInConstructor()
        {
            if (SymbolHelper.IsDeclaredOutsideTheSourcecode(property))
            {
                return  property.IsReadOnly ||  (property.SetMethod != null && new[] {Accessibility.Public, Accessibility.Protected}.Contains(property.SetMethod.DeclaredAccessibility));
            }

            if (property.SetMethod != null)
            {
                return true;
            }

            var propertyDeclaration = property.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
            if (propertyDeclaration?.AccessorList == null)
            {
                return false;
            }

            if (SymbolHelper.HasPrivateSetter(propertyDeclaration))
            {
                return false;
            }

            return propertyDeclaration.AccessorList.Accessors.Count == 1 && propertyDeclaration.AccessorList.Accessors.SingleOrDefault(IsAutoGetter) != null;
        }

        private static bool IsAutoGetter(AccessorDeclarationSyntax x)
        {
            return x.IsKind(SyntaxKind.GetAccessorDeclaration) && x.Body == null && x.ExpressionBody == null;
        }
    }
}