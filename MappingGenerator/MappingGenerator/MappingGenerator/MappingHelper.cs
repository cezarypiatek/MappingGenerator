using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator
{
    public static class MappingHelper
    {
        public static bool IsMappingBetweenCollections(ITypeSymbol targetClassSymbol, ITypeSymbol sourceClassSymbol)
        {
            return (ObjectHelper.HasInterface(targetClassSymbol, "System.Collections.ICollection") || targetClassSymbol.Kind == SymbolKind.ArrayType)
                   && (ObjectHelper.HasInterface(sourceClassSymbol, "System.Collections.IEnumerable") || sourceClassSymbol.Kind == SymbolKind.ArrayType);
        }

        public static ITypeSymbol GetElementType(ITypeSymbol collectionType)
        {
            switch (collectionType)
            {
                case INamedTypeSymbol namedType:
                    if (namedType.IsGenericType == false)
                    {
                        if (ObjectHelper.IsSystemObject(namedType.BaseType))
                        {
                            return namedType.BaseType;
                        }
                        return GetElementType(namedType.BaseType);
                    }
                    return namedType.TypeArguments[0];
                case IArrayTypeSymbol arrayType:
                    return arrayType.ElementType;
                default:
                    throw new NotSupportedException("Unknown collection type");
            }
        }

        public static InitializerExpressionSyntax FixInitializerExpressionFormatting(this InitializerExpressionSyntax initializerExpressionSyntax)
        {
            return initializerExpressionSyntax
                .WithLeadingTrivia(SyntaxTriviaList.Create(SyntaxFactory.EndOfLine(Environment.NewLine)));
        }
    }
}