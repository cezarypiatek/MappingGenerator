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
        private readonly IAssemblySymbol _contextAssembly;

        public ScaffoldingSourceFinder(SyntaxGenerator syntaxGenerator, Document document, IAssemblySymbol contextAssembly)
        {
            this.syntaxGenerator = syntaxGenerator;
            _document = document;
            _contextAssembly = contextAssembly;
        }

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            return FindMappingSource(targetType, new MappingPath());
        }

        private MappingElement FindMappingSource(ITypeSymbol targetType, MappingPath mappingPath)
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

            if (SymbolHelper.IsNullable(type, out var underlyingType))
            {
                type = underlyingType;
            }


            if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol namedTypeSymbol)
            {
                var enumOptions = namedTypeSymbol.MemberNames.Where(x=>x!="value__" && x!=".ctor").ToList();
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
                            return GetDefaultForUnknown(type, SyntaxFactory.ParseTypeName("object"));
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

                    var publicConstructors = nt.Constructors.Where(x =>
                        x.DeclaredAccessibility == Accessibility.Public ||
                        (x.DeclaredAccessibility == Accessibility.Internal &&
                         x.ContainingAssembly.IsSameAssemblyOrHasFriendAccessTo(_contextAssembly))).ToList();


                    var hasDefaultConstructor = publicConstructors.Any(x => x.Parameters.Length == 0);
                    if (hasDefaultConstructor == false && publicConstructors.Count > 0)
                    {
                        var randomConstructor = publicConstructors.First();
                        var constructorArguments = randomConstructor.Parameters.Select(p => GetDefaultExpression(p.Type, mappingPath.Clone())).ToList();
                        objectCreationExpression = (ObjectCreationExpressionSyntax)syntaxGenerator.ObjectCreationExpression(nt, constructorArguments);
                    }

                    var fields = ObjectHelper.GetFieldsThaCanBeSetPublicly(nt, _contextAssembly);
                    var assignments = fields.Select(x =>
                    {
                        var identifier = (ExpressionSyntax)(SyntaxFactory.IdentifierName(x.Name));
                        return (ExpressionSyntax)syntaxGenerator.AssignmentStatement(identifier, this.FindMappingSource(x.Type, mappingPath.Clone()).Expression);
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

        private SyntaxNode GetDefaultForSpecialType(ITypeSymbol type)
        {
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
                case SpecialType.System_DateTime:
                    return syntaxGenerator.MemberAccessExpression(SyntaxFactory.IdentifierName("DateTime"), "Now");
                case SpecialType.System_Object:
                    return SyntaxFactory.ObjectCreationExpression((TypeSyntax) syntaxGenerator.TypeExpression(type),
                        SyntaxFactory.ArgumentList(), default(InitializerExpressionSyntax));
                default:
                    return syntaxGenerator.LiteralExpression("/*TODO: provide value*/");
            }
        }

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