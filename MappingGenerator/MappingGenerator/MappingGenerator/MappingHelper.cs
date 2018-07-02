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
            return IsCollection(targetClassSymbol) && IsCollection(sourceClassSymbol);
        }

        public static bool IsCollection(ITypeSymbol typeSymbol)
        {
            return ObjectHelper.HasInterface(typeSymbol, "System.Collections.ICollection") || 
                   ObjectHelper.HasInterface(typeSymbol, "System.Collections.IEnumerable") || 
                   typeSymbol.Kind == SymbolKind.ArrayType;
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
            ITypeSymbol createdObjectTyp, SemanticModel semanticModel, SyntaxGenerator syntaxGenerator,
            MappingPath mappingPath=null)
        {
            var propertiesToSet = ObjectHelper.GetFieldsThaCanBeSetPublicly(createdObjectTyp);
            var assignments = MapUsingSimpleAssignment(syntaxGenerator, semanticModel, propertiesToSet, mappingSourceFinder, mappingPath);

            var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression,new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assignments )).FixInitializerExpressionFormatting(objectCreationExpression);
            return objectCreationExpression.WithInitializer(initializerExpressionSyntax);
        }


        public static IEnumerable<ExpressionSyntax> MapUsingSimpleAssignment(SyntaxGenerator generator,
            SemanticModel semanticModel, IEnumerable<IPropertySymbol> targets, IMappingSourceFinder sourceFinder,
            MappingPath mappingPath =null, SyntaxNode globalTargetAccessor = null)
        {
            if (mappingPath == null)
            {
                mappingPath = new MappingPath();
            }
            var mappingEngine = new MappingEngine(semanticModel, generator);
            return targets.Select(property => new
                {
                    source = sourceFinder.FindMappingSource(property.Name, property.Type),
                    target = new MappingElement()
                    {
                        Expression = (ExpressionSyntax) CreateAccessPropertyExpression(globalTargetAccessor, property, generator),
                        ExpressionType = property.Type
                    }
                })
                .Where(x=>x.source!=null)
                .Select(pair =>
                {
                    var sourceExpression = mappingEngine.MapExpression(pair.source, pair.target.ExpressionType, mappingPath.Clone()).Expression;
                    return (ExpressionSyntax) generator.AssignmentStatement(pair.target.Expression, sourceExpression);
                }).ToList();
        }

        private static SyntaxNode CreateAccessPropertyExpression(SyntaxNode globalTargetAccessor, IPropertySymbol property, SyntaxGenerator generator)
        {
            if (globalTargetAccessor == null)
            {
                return SyntaxFactory.IdentifierName(property.Name);
            }
            return generator.MemberAccessExpression(globalTargetAccessor, property.Name);
        }
    }
}