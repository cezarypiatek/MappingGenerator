using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

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

        public static ObjectCreationExpressionSyntax AddInitializerWithMapping(
            this ObjectCreationExpressionSyntax objectCreationExpression, IMappingSourceFinder mappingSourceFinder,
            ITypeSymbol createdObjectTyp)
        {
            var propertiesToSet = ObjectHelper.GetPublicPropertySymbols(createdObjectTyp).Where(x => x.SetMethod?.DeclaredAccessibility == Accessibility.Public);
            var assigments =  propertiesToSet.Select(x =>
            {
                var src = mappingSourceFinder.FindMappingSource(x.Name, x.Type);
                if (src != null)
                {
                    return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(x.Name), src.Expression);
                }

                return null;
            }).OfType<ExpressionSyntax>();

            var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression,new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assigments)).FixInitializerExpressionFormatting();
            return objectCreationExpression.WithInitializer(initializerExpressionSyntax);
        }

        public static ObjectCreationExpressionSyntax CreateObjectCreationExpressionWithInitializer(ITypeSymbol targetType, IMappingSourceFinder subMappingSourceFinder, SyntaxGenerator syntaxGenerator, SemanticModel semanticModel)
        {
            return ((ObjectCreationExpressionSyntax) syntaxGenerator.ObjectCreationExpression(targetType)).AddInitializerWithMapping(subMappingSourceFinder, targetType);
        }
    }
}