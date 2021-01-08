using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.Mappings
{
    public class MappingContext
    {
        public INamedTypeSymbol ContextSymbol { get; }

        public AccessibilityHelper AccessibilityHelper { get; }

        public MappingContext(INamedTypeSymbol contextSymbol)
        {
            ContextSymbol = contextSymbol;
            AccessibilityHelper = new AccessibilityHelper(contextSymbol);
        }

        public MappingContext(SyntaxNode contextExpression, SemanticModel semanticModel)
        {
            var typeDeclaration = contextExpression.FindContainer<TypeDeclarationSyntax>();
            ContextSymbol =  semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
            AccessibilityHelper = new AccessibilityHelper(ContextSymbol);
        }
        public bool WrapInCustomConversion { get; set; }

        public List<CustomConversion> CustomConversions { get; }  = new List<CustomConversion>();

        public CustomConversion? FindConversion(AnnotatedType fromType, AnnotatedType toType)
        {
            if (CustomConversions.Count == 0)
            {
                return null;
            }

            var candidates = CustomConversions.Where(x => x.FromType.Type.Equals(fromType.Type) && x.ToType.Type.Equals(toType.Type)).ToList();
            if (candidates.Count == 0)
            {
                return null;
            }
            if (candidates.Count == 1)
            {
                return candidates[0];
            }
            if (candidates.Count > 1)
            {
                var exactlyConversion = candidates.FirstOrDefault(x => x.FromType.CanBeNull == fromType.CanBeNull && x.ToType.CanBeNull == toType.CanBeNull);
                if (exactlyConversion != null)
                {
                    return exactlyConversion;
                }

                return candidates.FirstOrDefault(x => x.FromType.CanBeNull == fromType.CanBeNull || x.ToType.CanBeNull == toType.CanBeNull);
            }

            return candidates.FirstOrDefault();
        }
    }

    public class CustomConversion
    {
        public AnnotatedType FromType { get; set; }
        public AnnotatedType ToType { get; set; }

        public ExpressionSyntax Conversion { get; set; }
    }
}