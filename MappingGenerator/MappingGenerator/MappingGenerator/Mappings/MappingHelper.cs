using System;
using System.Linq;
using MappingGenerator.Features.Refactorings;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings
{
    public static class MappingHelper
    {
        public static bool IsMappingBetweenCollections(ITypeSymbol targetClassSymbol, ITypeSymbol sourceClassSymbol)
        {
            return IsCollection(targetClassSymbol) && IsCollection(sourceClassSymbol);
        }
        
        public static bool IsCollection(ITypeSymbol typeSymbol)
        {
            return typeSymbol.Kind == SymbolKind.ArrayType  || ObjectHelper.HasInterface(typeSymbol, "System.Collections.IEnumerable");
        }

        public static AnnotatedType GetElementType(ITypeSymbol collectionType)
        {
            switch (collectionType)
            {
                case INamedTypeSymbol namedType:
                    if (namedType.IsGenericType == false)
                    {
                        if (namedType.BaseType == null)
                        {
                            var indexer = namedType.GetMembers(WellKnownMemberNames.Indexer).OfType<IPropertySymbol>().FirstOrDefault();
                            if (indexer != null)
                            {
                               return  new AnnotatedType(indexer.Type);
                            }

                            throw new NotSupportedException("Cannot determine collection element type");
                        }
                        if (ObjectHelper.IsSystemObject(namedType.BaseType))
                        {
                            return new AnnotatedType(namedType.BaseType);
                        }
                        return GetElementType(namedType.BaseType);
                    }
                    return new AnnotatedType(namedType.TypeArguments[0]);
                case IArrayTypeSymbol arrayType:
                    return new AnnotatedType(arrayType.ElementType);
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

       

        public static SyntaxNode WrapInReadonlyCollectionIfNecessary(this SyntaxNode node, bool isReadonly,
            SyntaxGenerator generator)
        {
            if (isReadonly == false)
            {
                return node;
            }

            var accessAsReadonly = generator.MemberAccessExpression(node, "AsReadOnly");
            return generator.InvocationExpression(accessAsReadonly);
        }

        public static TypeDeclarationSyntax AddMethods(this TypeDeclarationSyntax typeDeclaration, MethodDeclarationSyntax[] newMethods)
        {
            var list = newMethods.ToList();

            var newMembers = typeDeclaration.Members.Select(x =>
            {
                if (x is MethodDeclarationSyntax md)
                {
                    foreach (var member in list)
                    {
                        //TODO:verify parameters list
                        //TODO:Check explicit interface implementation
                        if (md.Identifier.Text == member.Identifier.Text && 
                            md.ReturnType.ToString() == member.ReturnType.ToString() &&
                            md.ParameterList.Parameters.Count == member.ParameterList.Parameters.Count)
                        {
                            list.Remove(member);
                            return member;
                        }
                    }
                }
                return x;
            });

            return typeDeclaration.WithMembers(newMembers.Concat(list));
        }
    }
}