using System.Linq;
using MappingGenerator.Mappings;
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
            Type = new AnnotatedType(property.Type);
        }

        public string Name => property.Name;

        public AnnotatedType Type { get; }

        public bool CanBeSet(ITypeSymbol via, MappingContext mappingContext)
        {
            
            if(property.SetMethod == null)
            {
                //Handle properties that can be set via { }
                return CanBeSetOnlyIndirectly(via, mappingContext);
            }

            return mappingContext.AccessibilityHelper.IsSymbolAccessible(property.SetMethod, via);
        }

        public bool CanBeSetOnlyIndirectly(ITypeSymbol via, MappingContext mappingContext)
        {
            if (property.SetMethod != null)
            {
                return false;
            }

            if (ObjectHelper.IsSimpleType(property.Type) || SymbolHelper.IsNullable(property.Type))
            {
                return false;
            }

            return mappingContext.AccessibilityHelper.IsSymbolAccessible(property, via);
        }


        public bool CanBeSetInConstructor(ITypeSymbol via, MappingContext mappingContext)
        {
            if (CanBeSet(via, mappingContext))
            {
                return true;
            }

            if (SymbolHelper.IsDeclaredOutsideTheSourcecode(property))
            {
                return  property.IsReadOnly ||  (property.SetMethod != null && new[] {Accessibility.Public, Accessibility.Protected}.Contains(property.SetMethod.DeclaredAccessibility));
            }
            
            var propertyDeclaration = property.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
            if (propertyDeclaration?.AccessorList == null)
            {
                return false;
            }

            if (HasPrivateSetter(propertyDeclaration))
            {
                if (property.ContainingType.Equals(via) == false)
                {
                    return false;
                }

                return true;
            }

            return propertyDeclaration.AccessorList.Accessors.Count == 1 && propertyDeclaration.AccessorList.Accessors.SingleOrDefault(IsAutoGetter) != null;
        }

        private static bool HasPrivateSetter(PropertyDeclarationSyntax propertyDeclaration)
        {
            return propertyDeclaration.AccessorList.Accessors.Any(x => x.Keyword.Kind() == SyntaxKind.SetKeyword && x.Modifiers.Any(m => m.Kind() == SyntaxKind.PrivateKeyword));
        }

        private static bool IsAutoGetter(AccessorDeclarationSyntax x)
        {
            return x.IsKind(SyntaxKind.GetAccessorDeclaration) && x.Body == null && x.ExpressionBody == null;
        }

        public bool CanBeGet(ITypeSymbol via, MappingContext mappingContext)
        {
            if (property.GetMethod == null)
            {
                return false;
            }

            return mappingContext.AccessibilityHelper.IsSymbolAccessible(property.GetMethod, via);
        }
    }
}