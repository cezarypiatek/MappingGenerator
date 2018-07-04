using System;
using System.Collections.Generic;
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

        public static InitializerExpressionSyntax FixInitializerExpressionFormatting(this InitializerExpressionSyntax initializerExpressionSyntax, ObjectCreationExpressionSyntax objectCreationExpression)
        {
            var trivia = objectCreationExpression.ArgumentList?.CloseParenToken.TrailingTrivia ?? objectCreationExpression.Type.GetTrailingTrivia();
            if (trivia.ToFullString().Contains(Environment.NewLine))
            {
                return initializerExpressionSyntax;
            }
            return initializerExpressionSyntax
                .WithLeadingTrivia(SyntaxTriviaList.Create(SyntaxFactory.EndOfLine(Environment.NewLine)));
        }

        public static ObjectCreationExpressionSyntax AddInitializerWithMapping(
            this ObjectCreationExpressionSyntax objectCreationExpression, IMappingSourceFinder mappingSourceFinder,
            ITypeSymbol createdObjectTyp, SemanticModel semanticModel, SyntaxGenerator syntaxGenerator)
        {
            var propertiesToSet = ObjectHelper.GetFieldsThaCanBeSetPublicly(createdObjectTyp);
            var assigments = MapUsingSimpleAssigment(syntaxGenerator, semanticModel, propertiesToSet, mappingSourceFinder);

            var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression,new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assigments)).FixInitializerExpressionFormatting(objectCreationExpression);
            return objectCreationExpression.WithInitializer(initializerExpressionSyntax);
        }


        public static IEnumerable<ExpressionSyntax> MapUsingSimpleAssigment(SyntaxGenerator generator, SemanticModel semanticModel, IEnumerable<IPropertySymbol> targets, IMappingSourceFinder sourceFinder,  SyntaxNode gloablTargetAccessor = null)
        {
            var mappingEngie = new MappingEngine(semanticModel, generator);
            return targets.Select(property => new
                {
                    source = sourceFinder.FindMappingSource(property.Name, property.Type),
                    target = new MappingElement()
                    {
                        Expression = (ExpressionSyntax) CreateAccessPropertyExpression(gloablTargetAccessor, property, generator),
                        ExpressionType = property.Type
                    }
                })
                .Where(x=>x.source!=null)
                .Select(pair =>
                {
                    var sourceExpression = mappingEngie.MapExpression(pair.source, pair.target.ExpressionType).Expression;
                    return (ExpressionSyntax) generator.AssignmentStatement(pair.target.Expression, sourceExpression);
                }).ToList();
        }

        private static SyntaxNode CreateAccessPropertyExpression(SyntaxNode gloablTargetAccessor, IPropertySymbol property, SyntaxGenerator generator)
        {
            if (gloablTargetAccessor == null)
            {
                return SyntaxFactory.IdentifierName(property.Name);
            }
            return generator.MemberAccessExpression(gloablTargetAccessor, property.Name);
        }


        public static ObjectCreationExpressionSyntax CreateObjectCreationExpressionWithInitializer(ITypeSymbol targetType, IMappingSourceFinder subMappingSourceFinder, SyntaxGenerator syntaxGenerator, SemanticModel semanticModel)
        {
            return ((ObjectCreationExpressionSyntax) syntaxGenerator.ObjectCreationExpression(targetType)).AddInitializerWithMapping(subMappingSourceFinder, targetType, semanticModel, syntaxGenerator);
        }
    }
}