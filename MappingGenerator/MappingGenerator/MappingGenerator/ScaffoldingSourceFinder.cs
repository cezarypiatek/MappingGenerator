using System;
using System.Collections;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public class ScaffoldingSourceFinder:IMappingSourceFinder
    {
        private readonly SyntaxGenerator syntaxGenerator;

        public ScaffoldingSourceFinder(SyntaxGenerator syntaxGenerator)
        {
            this.syntaxGenerator = syntaxGenerator;
        }

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            return FindMappingSource(targetName, targetType, new MappingPath());
        }

        private MappingElement FindMappingSource(string targetName, ITypeSymbol targetType, MappingPath mappingPath)
        {
            return new MappingElement
            {
                ExpressionType = targetType,
                Expression = (ExpressionSyntax) GetDefaultExpression(targetType, mappingPath)
            };  
        }

        internal SyntaxNode GetDefaultExpression(ITypeSymbol type, MappingPath mappingPath)
        {
            if (mappingPath.AddToMapped(type) == false)
            {
                return syntaxGenerator.DefaultExpression(type)
                    .WithTrailingTrivia(SyntaxFactory.Comment(" /* Stop recursive mapping */"));
            }

            //TODO: Handle types without default constructor
            //TODO: Handle ReadOnlyCollection

            if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol namedTypeSymbol)
            {
                var enumOptions = namedTypeSymbol.MemberNames.ToList();
                if (enumOptions.Count > 0)
                {
                    return syntaxGenerator.MemberAccessExpression(syntaxGenerator.IdentifierName(namedTypeSymbol.Name), syntaxGenerator.IdentifierName(enumOptions[0]));
                }
                return syntaxGenerator.DefaultExpression(type);
            }

            if (type.SpecialType == SpecialType.None)
            {
                var objectCreationExpression = (ObjectCreationExpressionSyntax)syntaxGenerator.ObjectCreationExpression(type);

                if (MappingHelper.IsCollection(type))
                {
                    if (type is IArrayTypeSymbol)
                    {
                        objectCreationExpression = SyntaxFactory.ObjectCreationExpression((TypeSyntax)syntaxGenerator.TypeExpression(type));
                    }
                    else if (type.TypeKind == TypeKind.Interface)
                    {
                        var namedType = type as INamedTypeSymbol;
                        if (namedType.IsGenericType)
                        {
                            var typeArgumentListSyntax = SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(namedType.TypeArguments.Select(x=> syntaxGenerator.TypeExpression(x))));
                            var newType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("List"), typeArgumentListSyntax);
                            objectCreationExpression = SyntaxFactory.ObjectCreationExpression(newType, SyntaxFactory.ArgumentList(), default(InitializerExpressionSyntax));
                        }
                        else
                        {
                            var newType = SyntaxFactory.ParseTypeName("ArrayList");
                            objectCreationExpression = SyntaxFactory.ObjectCreationExpression(newType, SyntaxFactory.ArgumentList(), default(InitializerExpressionSyntax));
                        }
                    }

                    var subType = MappingHelper.GetElementType(type);
                    var initializationBlockExpressions = new SeparatedSyntaxList<ExpressionSyntax>();
                    var subTypeDefault = (ExpressionSyntax)GetDefaultExpression(subType, mappingPath.Clone());
                    if (subTypeDefault != null)
                    {
                        initializationBlockExpressions = initializationBlockExpressions.Add(subTypeDefault);
                    }

                    var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, initializationBlockExpressions).FixInitializerExpressionFormatting(objectCreationExpression);
                    return objectCreationExpression
                        .WithInitializer(initializerExpressionSyntax);
                }

                {

                    var fields = ObjectHelper.GetFieldsThaCanBeSetPublicly(type);
                    var assignments = fields.Select(x =>
                    {
                        var identifier = (ExpressionSyntax)(SyntaxFactory.IdentifierName(x.Name));
                        return (ExpressionSyntax)syntaxGenerator.AssignmentStatement(identifier, this.FindMappingSource(x.Name, x.Type, mappingPath).Expression);
                    });
                    var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assignments)).FixInitializerExpressionFormatting(objectCreationExpression);
                    return objectCreationExpression.WithInitializer(initializerExpressionSyntax);
                }

                
            }


            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return syntaxGenerator.LiteralExpression(true);
                case SpecialType.System_SByte:
                    return syntaxGenerator.LiteralExpression(1);
                case SpecialType.System_Int16:
                    return  syntaxGenerator.LiteralExpression(16);
                case SpecialType.System_Int32:
                    return syntaxGenerator.LiteralExpression(32);
                case SpecialType.System_Int64:
                    return syntaxGenerator.LiteralExpression(64);
                case SpecialType.System_Byte:
                    return syntaxGenerator.LiteralExpression(1);
                case SpecialType.System_UInt16:
                    return syntaxGenerator.LiteralExpression(16u);
                case SpecialType.System_UInt32:
                    return syntaxGenerator.LiteralExpression(32u);
                case SpecialType.System_UInt64:
                    return syntaxGenerator.LiteralExpression(64u);
                case SpecialType.System_Single:
                    return syntaxGenerator.LiteralExpression(1.0f);
                case SpecialType.System_Double:
                    return syntaxGenerator.LiteralExpression(1.0);
                case SpecialType.System_Char:
                    return syntaxGenerator.LiteralExpression('a');
                case SpecialType.System_String:
                    return syntaxGenerator.LiteralExpression("lorem ipsum");
                case SpecialType.System_Decimal:
                    return syntaxGenerator.LiteralExpression(2.0m);
                case SpecialType.System_Object:
                    return SyntaxFactory.ObjectCreationExpression((TypeSyntax)syntaxGenerator.TypeExpression(type), SyntaxFactory.ArgumentList(),default(InitializerExpressionSyntax));
                default:
                    return syntaxGenerator.LiteralExpression("ccc");
            }
        }

    }

    public static class TypeSyntaxFactory
    {
        /// <summary>
        /// Used to generate a type without generic arguments
        /// </summary>
        /// <param name="identifier">The name of the type to be generated</param>
        /// <returns>An instance of TypeSyntax from the Roslyn Model</returns>
        public static TypeSyntax GetTypeSyntax(string identifier)
        {
            return
                SyntaxFactory.IdentifierName(
                    SyntaxFactory.Identifier(identifier)
                );
        }

        /// <summary>
        /// Used to generate a type with generic arguments
        /// </summary>
        /// <param name="identifier">Name of the Generic Type</param>
        /// <param name="arguments">
        /// Types of the Generic Arguments, which must be basic identifiers
        /// </param>
        /// <returns>An instance of TypeSyntax from the Roslyn Model</returns>
        public static TypeSyntax GetTypeSyntax(string identifier, params string[] arguments)
        {
            return GetTypeSyntax(identifier, arguments.Select(GetTypeSyntax).ToArray());
        }

        /// <summary>
        /// Used to generate a type with generic arguments
        /// </summary>
        /// <param name="identifier">Name of the Generic Type</param>
        /// <param name="arguments">
        /// Types of the Generic Arguments, which themselves may be generic types
        /// </param>
        /// <returns>An instance of TypeSyntax from the Roslyn Model</returns>
        public static TypeSyntax GetTypeSyntax(string identifier, params TypeSyntax[] arguments)
        {
            return
                SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier(identifier),
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SeparatedList(
                            arguments.Select(
                                x =>
                                {
                                    if (x is GenericNameSyntax)
                                    {
                                        var gen_x = x as GenericNameSyntax;
                                        return
                                            GetTypeSyntax(
                                                gen_x.Identifier.ToString(),
                                                gen_x.TypeArgumentList.Arguments.ToArray()
                                            );
                                    }
                                    else
                                    {
                                        return x;
                                    }
                                }
                            )
                        )
                    )
                );
        }
    }
}