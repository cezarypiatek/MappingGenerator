using System.Collections.Generic;
using System.Linq;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;

namespace MappingGenerator.Mappings.SourceFinders
{
    public class ScaffoldingSourceFinder:IMappingSourceFinder
    {
        private readonly SyntaxGenerator syntaxGenerator;
        private readonly Document _document;

        public ScaffoldingSourceFinder(SyntaxGenerator syntaxGenerator, Document document)
        {
            this.syntaxGenerator = syntaxGenerator;
            _document = document;
        }

        public MappingElement FindMappingSource(string targetName, AnnotatedType targetType, MappingContext mappingContext)
        {
            return FindMappingSource(targetType, mappingContext, new MappingPath());
        }

        private MappingElement FindMappingSource(AnnotatedType targetType, MappingContext mappingContext, MappingPath mappingPath)
        {
            return new MappingElement
            {
                ExpressionType = targetType,
                Expression = (ExpressionSyntax) GetDefaultExpression(targetType.Type, mappingContext, mappingPath)
            };  
        }

        internal SyntaxNode GetDefaultExpression(ITypeSymbol type, MappingContext mappingContext, MappingPath mappingPath)
        {
            if (mappingPath.AddToMapped(type) == false)
            {
                return syntaxGenerator.DefaultExpression(type)
                    .WithTrailingTrivia(SyntaxFactory.Comment(" /* Stop recursive mapping */"));
            }

            if (SymbolHelper.IsNullable(type, out var underlyingType))
            {
                type = underlyingType;
            }


            if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol namedTypeSymbol)
            {
                var enumOptions = namedTypeSymbol.MemberNames.Where(x=>x!="value__" && x!=".ctor").OrderBy(x=>x).ToList();
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
                    var isReadonlyCollection = ObjectHelper.IsReadonlyCollection(type);

                    if (type is IArrayTypeSymbol)
                    {
                        objectCreationExpression = SyntaxFactory.ObjectCreationExpression((TypeSyntax)syntaxGenerator.TypeExpression(type));
                    }
                    else if (type.TypeKind == TypeKind.Interface || isReadonlyCollection)
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
                            var newType = SyntaxFactory. ParseTypeName("ArrayList");
                            objectCreationExpression = SyntaxFactory.ObjectCreationExpression(newType, SyntaxFactory.ArgumentList(), default(InitializerExpressionSyntax));
                        }
                    }
                    var subType = MappingHelper.GetElementType(type);
                    var initializationBlockExpressions = new SeparatedSyntaxList<ExpressionSyntax>();
                    var subTypeDefault = (ExpressionSyntax)GetDefaultExpression(subType.Type, mappingContext, mappingPath.Clone());
                    if (subTypeDefault != null)
                    {
                        initializationBlockExpressions = initializationBlockExpressions.Add(subTypeDefault);
                    }

                    var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, initializationBlockExpressions).FixInitializerExpressionFormatting(objectCreationExpression);
                    return objectCreationExpression
                        .WithInitializer(initializerExpressionSyntax)
                        .WrapInReadonlyCollectionIfNecessary(isReadonlyCollection, syntaxGenerator);
                }

                {
                    var nt = type as INamedTypeSymbol;

                    if (nt == null)
                    {
                        var genericTypeConstraints = type.UnwrapGeneric().ToList();
                        if (genericTypeConstraints.Any() == false)
                        {
                            return GetDefaultForUnknown(type, ObjectType);
                        }
                        nt =  genericTypeConstraints.FirstOrDefault(x=>x.TypeKind == TypeKind.Class) as INamedTypeSymbol ??
                              genericTypeConstraints.FirstOrDefault(x => x.TypeKind == TypeKind.Interface) as INamedTypeSymbol;
                    }

                    if (nt == null)
                    {
                        return GetDefaultForUnknownType(type);
                    }

                    if (nt.TypeKind == TypeKind.Interface)
                    {
                        var implementations =  SymbolFinder.FindImplementationsAsync(type, this._document.Project.Solution).Result;
                        var firstImplementation = implementations.FirstOrDefault();
                        if (firstImplementation is INamedTypeSymbol == false)
                        {
                            return GetDefaultForUnknownType(type);
                        }

                        nt = firstImplementation as INamedTypeSymbol;
                        objectCreationExpression = (ObjectCreationExpressionSyntax) syntaxGenerator.ObjectCreationExpression(nt);

                    }else if (nt.TypeKind == TypeKind.Class && nt.IsAbstract)
                    {
                        var randomDerived = SymbolFinder.FindDerivedClassesAsync(nt, this._document.Project.Solution).Result
                            .FirstOrDefault(x => x.IsAbstract == false);
                        
                        if (randomDerived != null)
                        {
                            nt = randomDerived;
                            objectCreationExpression = (ObjectCreationExpressionSyntax)syntaxGenerator.ObjectCreationExpression(nt);
                        }
                    }

                    var publicConstructors = nt.Constructors.Where(x => mappingContext.AccessibilityHelper.IsSymbolAccessible(x, nt)).ToList();
                    var hasDefaultConstructor = publicConstructors.Any(x => x.Parameters.Length == 0);
                    if (hasDefaultConstructor == false && publicConstructors.Count > 0)
                    {
                        var randomConstructor = publicConstructors.First();
                        var constructorArguments = randomConstructor.Parameters.Select(p => GetDefaultExpression(p.Type, mappingContext, mappingPath.Clone())).ToList();
                        objectCreationExpression = (ObjectCreationExpressionSyntax)syntaxGenerator.ObjectCreationExpression(nt, constructorArguments);
                    }

                    var fields = MappingTargetHelper.GetFieldsThaCanBeSetPublicly(nt, mappingContext);
                    var assignments = fields.Select(x =>
                    {
                        var identifier = (ExpressionSyntax)(SyntaxFactory.IdentifierName(x.Name));
                        var mappingSource = this.FindMappingSource(x.Type, mappingContext, mappingPath.Clone());
                        return (ExpressionSyntax)syntaxGenerator.AssignmentStatement(identifier, mappingSource.Expression);
                    }).ToList();

                    if (assignments.Count == 0)
                    {
                        return objectCreationExpression;
                    }
                    var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assignments)).FixInitializerExpressionFormatting(objectCreationExpression);
                    return objectCreationExpression.WithInitializer(initializerExpressionSyntax);
                }
            }


            return GetDefaultForSpecialType(type);
        }


        private static readonly SyntaxNode UnknownDefault = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("/*TODO: provide value*/"));
        private static readonly PredefinedTypeSyntax ObjectType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword));

        private static readonly Dictionary<SpecialType, SyntaxNode> WellKnowDefaults = new Dictionary<SpecialType, SyntaxNode>()
        {
                [SpecialType.System_Boolean] = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression),
                [SpecialType.System_SByte] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1)),
                [SpecialType.System_Int16] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(16)),
                [SpecialType.System_Int32] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(32)),
                [SpecialType.System_Int64] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(64)),
                [SpecialType.System_Byte] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1)),
                [SpecialType.System_UInt16] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(16u)),
                [SpecialType.System_UInt32] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(32u)),
                [SpecialType.System_UInt64] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(64u)),
                [SpecialType.System_Single] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1.0f)),
                [SpecialType.System_Double] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1.0)),
                [SpecialType.System_Char] = SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal('a')),
                [SpecialType.System_String] = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("lorem ipsum")),
                [SpecialType.System_Decimal] = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(2.0m)),
                [SpecialType.System_DateTime] = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,SyntaxFactory.IdentifierName("DateTime"), SyntaxFactory.IdentifierName("Now")),
                [SpecialType.System_Object] = SyntaxFactory.ObjectCreationExpression(ObjectType).WithArgumentList(SyntaxFactory.ArgumentList())
        };

        private SyntaxNode GetDefaultForSpecialType(ITypeSymbol type) => WellKnowDefaults.TryGetValue(type.SpecialType, out var res) ? res : UnknownDefault;

        private SyntaxNode GetDefaultForUnknownType(ITypeSymbol type)
        {
            return syntaxGenerator.DefaultExpression(type)
                .WithTrailingTrivia(SyntaxFactory.Comment($" /* Cannot find any type implementing {type.Name} */"));
        }

        private SyntaxNode GetDefaultForUnknown(ITypeSymbol type, TypeSyntax objectType)
        {
            return syntaxGenerator.DefaultExpression(objectType)
                .WithTrailingTrivia(SyntaxFactory.Comment($" /* Cannot find any type implementing {type.Name} */"));
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