using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public Task<MappingElement> FindMappingSource(string targetName, AnnotatedType targetType, MappingContext mappingContext)
        {
            return FindMappingSource(targetType, mappingContext, new MappingPath());
        }

        private async Task<MappingElement> FindMappingSource(AnnotatedType targetType, MappingContext mappingContext, MappingPath mappingPath)
        {
            return new MappingElement
            {
                ExpressionType = targetType,
                Expression = (ExpressionSyntax)(await GetDefaultExpression(targetType.Type, mappingContext, mappingPath).ConfigureAwait(false))
            };  
        }

        private async Task<SyntaxNode> GetDefaultExpression(ITypeSymbol type, MappingContext mappingContext, MappingPath mappingPath)
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
                var enumOption = namedTypeSymbol.MemberNames.Where(x => x != "value__" && x != ".ctor").OrderBy(x => x).FirstOrDefault();
                if (enumOption != null)
                {
                    return SyntaxFactoryExtensions.CreateMemberAccessExpression(SyntaxFactory.IdentifierName(namedTypeSymbol.Name), false, enumOption);
                }
                return syntaxGenerator.DefaultExpression(type);
            }

            if (type.SpecialType == SpecialType.None)
            {


                ObjectCreationExpressionSyntax objectCreationExpression = null;

                if (MappingHelper.IsCollection(type))
                {
                    var isReadonlyCollection = ObjectHelper.IsReadonlyCollection(type);

                    if (type is IArrayTypeSymbol)
                    {
                        objectCreationExpression = CreateObject(type);
                    }
                    else if (type.TypeKind == TypeKind.Interface || isReadonlyCollection)
                    {
                        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
                        {
                            var typeArgumentListSyntax = SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(namedType.TypeArguments.Select(x => syntaxGenerator.TypeExpression(x))));
                            var newType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("List"), typeArgumentListSyntax);
                            objectCreationExpression = SyntaxFactory.ObjectCreationExpression(newType, SyntaxFactory.ArgumentList(), default(InitializerExpressionSyntax));
                        }
                        else
                        {
                            var newType = SyntaxFactory.ParseTypeName("ArrayList");
                            objectCreationExpression = SyntaxFactory.ObjectCreationExpression(newType, SyntaxFactory.ArgumentList(), default(InitializerExpressionSyntax));
                        }
                    }
                    objectCreationExpression ??= CreateObject(type, Array.Empty<ArgumentSyntax>());

                    var subType = MappingHelper.GetElementType(type);
                    var initializationBlockExpressions = new SeparatedSyntaxList<ExpressionSyntax>();
                    var subTypeDefault = (ExpressionSyntax) (await GetDefaultExpression(subType.Type, mappingContext, mappingPath.Clone()).ConfigureAwait(false));
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
                        nt = genericTypeConstraints.FirstOrDefault(x => x.TypeKind == TypeKind.Class) as INamedTypeSymbol ??
                              genericTypeConstraints.FirstOrDefault(x => x.TypeKind == TypeKind.Interface) as INamedTypeSymbol;
                    }

                    if (nt == null)
                    {
                        return GetDefaultForUnknownType(type);
                    }

                    if (nt.TypeKind == TypeKind.Interface)
                    {
                        var implementations = await SymbolFinder.FindImplementationsAsync(type, _document.Project.Solution).ConfigureAwait(false);
                        var firstImplementation = implementations.FirstOrDefault();
                        if (firstImplementation is INamedTypeSymbol == false)
                        {
                            return GetDefaultForUnknownType(type);
                        }

                        nt = firstImplementation as INamedTypeSymbol;
                        objectCreationExpression = CreateObject(nt);

                    }
                    else if (nt.TypeKind == TypeKind.Class && nt.IsAbstract)
                    {
                        var allDerived = await SymbolFinder.FindDerivedClassesAsync(nt, _document.Project.Solution).ConfigureAwait(false);
                        var randomDerived = allDerived.FirstOrDefault(x => x.IsAbstract == false);

                        if (randomDerived != null)
                        {
                            nt = randomDerived;
                            objectCreationExpression = CreateObject(nt);
                        }
                    }
                    else
                    {
                        var publicConstructors = nt.Constructors.Where(x => mappingContext.AccessibilityHelper.IsSymbolAccessible(x, nt)).ToList();
                        var hasDefaultConstructor = publicConstructors.Any(x => x.Parameters.Length == 0);
                        if (hasDefaultConstructor == false && publicConstructors.Count > 0)
                        {
                            var randomConstructor = publicConstructors.First();
                            var constructorArguments = await GetConstructorArguments(mappingContext, mappingPath, randomConstructor).ConfigureAwait(false);
                            objectCreationExpression = CreateObject(nt, constructorArguments);
                        }
                    }

                    var fields = mappingTargetHelper.GetFieldsThaCanBeSetPublicly(nt, mappingContext);
                    var assignments = new List<AssignmentExpressionSyntax>(fields.Count);
                    foreach (var x in fields)
                    {
                        var identifier = (ExpressionSyntax)(SyntaxFactory.IdentifierName(x.Name));
                        var mappingSource = await this.FindMappingSource(x.Type, mappingContext, mappingPath.Clone()).ConfigureAwait(false);
                        assignments.Add(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, identifier, mappingSource.Expression));
                    }

                    if (objectCreationExpression == null)
                    {
                        objectCreationExpression = CreateObject(type);
                    }

                    return SyntaxFactoryExtensions.WithMembersInitialization(objectCreationExpression, assignments);
                }
            }
            return GetDefaultForSpecialType(type);
        }

        private readonly MappingTargetHelper mappingTargetHelper = new MappingTargetHelper();

        private async Task<List<ArgumentSyntax>> GetConstructorArguments(MappingContext mappingContext, MappingPath mappingPath, IMethodSymbol randomConstructor)
        {
            var arguments = new List<ArgumentSyntax>(randomConstructor.Parameters.Length);
            foreach (var p in randomConstructor.Parameters)
            {
                var x = await GetDefaultExpression(p.Type, mappingContext, mappingPath.Clone()).ConfigureAwait(false);
                arguments.Add(SyntaxFactory.Argument((ExpressionSyntax)x));
            }
            return arguments;
        }

        private ObjectCreationExpressionSyntax CreateObject(ITypeSymbol type, IReadOnlyList<ArgumentSyntax> constructorArguments = null)
        {
            var argumentListSyntax = constructorArguments == null ?  null : SyntaxFactory.ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>().AddRange(constructorArguments));
            return SyntaxFactory.ObjectCreationExpression((TypeSyntax)syntaxGenerator.TypeExpression(type.StripNullability())).WithArgumentList(argumentListSyntax);
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