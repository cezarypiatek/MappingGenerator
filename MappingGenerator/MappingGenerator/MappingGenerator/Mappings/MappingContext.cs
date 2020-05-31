using System.Collections.Generic;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.Mappings
{
    public class MappingContext
    {
        public INamedTypeSymbol ContextSymbol { get; }

        public MappingContext(INamedTypeSymbol contextSymbol)
        {
            ContextSymbol = contextSymbol;
        }

        public MappingContext(SyntaxNode contextExpression, SemanticModel semanticModel)
        {
            var typeDeclaration = contextExpression.FindContainer<TypeDeclarationSyntax>();
            ContextSymbol =  semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
        }

        public HashSet<MappingType> MissingConversions { get; } = new HashSet<MappingType>();

        public void AddMissingConversion(ITypeSymbol fromType, ITypeSymbol toType) => MissingConversions.Add(
            new MappingType()
            {
                FromType = fromType,
                ToType = toType
            });

        public bool WrapInCustomConversion { get; set; }

        public Dictionary<(ITypeSymbol fromType, ITypeSymbol toType), ExpressionSyntax> CustomConversions { get; }  = new Dictionary<(ITypeSymbol fromType, ITypeSymbol toType), ExpressionSyntax>();

        public ExpressionSyntax? FindConversion(ITypeSymbol fromType, ITypeSymbol toType)
        {
            if (CustomConversions.TryGetValue((fromType, toType), out var conversion))
            {
                return conversion;
            }

            return null;
        }
    }
}