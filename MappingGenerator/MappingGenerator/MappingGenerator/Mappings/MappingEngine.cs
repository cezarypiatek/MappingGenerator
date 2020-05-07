using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Mappings.MappingMatchers;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.MethodHelpers;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings
{
    public class MappingEngine
    {
        protected readonly SemanticModel semanticModel;
        protected readonly SyntaxGenerator syntaxGenerator;
        protected readonly IAssemblySymbol contextAssembly;


        public MappingEngine(SemanticModel semanticModel, SyntaxGenerator syntaxGenerator, IAssemblySymbol contextAssembly)
        {
            this.semanticModel = semanticModel;
            this.syntaxGenerator = syntaxGenerator;
            this.contextAssembly = contextAssembly;
        }

        public TypeInfo GetExpressionTypeInfo(SyntaxNode expression)
        {
            return semanticModel.GetTypeInfo(expression);
        }

        public static async Task<MappingEngine> Create(Document document, CancellationToken cancellationToken, IAssemblySymbol contextAssembly)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            return new MappingEngine(semanticModel, syntaxGenerator, contextAssembly);
        }

        public MappingForeachElement MapUsingForeachExpression(string sourceName, ITypeSymbol sourceType, ITypeSymbol destinationType, MappingContext mappingContext)
        {
            var element = new MappingForeachElement();

            var destinationVariableName = NameHelper.ToLocalVariableName(destinationType.Name);
            if (sourceName == destinationVariableName)
            {
                destinationVariableName += "Target";
            }
            element.TargetName = destinationVariableName;

            var initializer = MapExpression(SyntaxFactory.IdentifierName(sourceName), sourceType, destinationType, mappingContext, skipCollections: true);
            element.SyntaxNodes.Add(syntaxGenerator.LocalDeclarationStatement(element.TargetName, initializer));

            var sourceProperties = ObjectHelper.GetPublicPropertySymbols(sourceType).ToList();

            foreach (var item in ObjectHelper.GetCollectionProperties(destinationType, contextAssembly))
            {
                var sourceProperty = sourceProperties.First(x => x.Name == item.Name);
                var sourceElementType = MappingHelper.GetElementType(sourceProperty.Type);
                var destinationElementType = MappingHelper.GetElementType(item.Type);

                var sourceMemberAccess = syntaxGenerator.MemberAccessExpression(SyntaxFactory.IdentifierName(NameHelper.ToLocalVariableName(sourceName)), item.Name);
                var targetMemberAccess = syntaxGenerator.MemberAccessExpression(SyntaxFactory.IdentifierName(element.TargetName), item.Name);

                var subStatementSyntaxNodes = new List<SyntaxNode>();
                if (ObjectHelper.IsSimpleType(sourceElementType) && ObjectHelper.IsSimpleType(destinationElementType))
                {
                    var addMemberAccess = syntaxGenerator.MemberAccessExpression(targetMemberAccess, "AddRange");
                    element.SyntaxNodes.Add(SyntaxFactory.ExpressionStatement((ExpressionSyntax)syntaxGenerator.InvocationExpression(addMemberAccess, sourceMemberAccess)));
                }
                else if (!ObjectHelper.IsSimpleType(sourceElementType) && !ObjectHelper.IsSimpleType(destinationElementType))
                {
                    var foreachVariableName = NameHelper.CreateLambdaParameterName(item.Name);
                    var subElement = MapUsingForeachExpression(foreachVariableName, sourceElementType, destinationElementType, mappingContext);
                    subStatementSyntaxNodes.AddRange(subElement.SyntaxNodes);

                    var addMemberAccess = syntaxGenerator.MemberAccessExpression(targetMemberAccess, "Add");
                    subStatementSyntaxNodes.Add(SyntaxFactory.ExpressionStatement((ExpressionSyntax)syntaxGenerator.InvocationExpression(addMemberAccess, SyntaxFactory.IdentifierName(subElement.TargetName))));

                    if (MappingHelper.IsDictionary(sourceProperty.Type))
                    {
                        sourceMemberAccess = syntaxGenerator.MemberAccessExpression(sourceMemberAccess, "Values");
                    }

                    element.SyntaxNodes.Add(SyntaxFactory.ForEachStatement(
                        SyntaxFactory.IdentifierName("var"),
                        SyntaxFactory.Identifier(foreachVariableName),
                        (ExpressionSyntax)sourceMemberAccess,
                        SyntaxFactory.Block(subStatementSyntaxNodes.Cast<StatementSyntax>())));
                }
            }

            return element;
        }

        public ExpressionSyntax MapExpression(ExpressionSyntax sourceExpression, ITypeSymbol sourceType,
            ITypeSymbol destinationType, MappingContext mappingContext, bool skipCollections = false)
        {
            var mappingSource = new MappingElement
            {
                Expression = sourceExpression,
                ExpressionType = sourceType
            };
            return MapExpression(mappingSource, destinationType, mappingContext, skipCollections).Expression;
        }

        public MappingElement MapExpression(MappingElement source, ITypeSymbol targetType, MappingContext mappingContext, bool skipCollections = false, MappingPath mappingPath = null)
        {
            if (source == null)
            {
                return null;
            }

            if (mappingPath == null)
            {
                mappingPath = new MappingPath();
            }

            var sourceType = source.ExpressionType;
            if (mappingPath.AddToMapped(sourceType) == false)
            {
                return new MappingElement()
                {
                    ExpressionType = sourceType,
                    Expression = source.Expression.WithTrailingTrivia(SyntaxFactory.Comment(" /* Stop recursive mapping */"))
                };
            }


            if (mappingContext.FindConversion(sourceType, targetType) is {} userDefinedConversion)
            {
                return new MappingElement()
                {
                    ExpressionType = targetType,
                    Expression = (ExpressionSyntax) syntaxGenerator.InvocationExpression(userDefinedConversion, source.Expression)
                };
            }


            if (ObjectHelper.IsSimpleType(targetType) && SymbolHelper.IsNullable(sourceType, out var underlyingType) )
            {
                source = new MappingElement()
                {
                    Expression =  (ExpressionSyntax)syntaxGenerator.MemberAccessExpression(source.Expression, "Value"),
                    ExpressionType = underlyingType
                };
            }

            if (IsUnwrappingNeeded(targetType, source))
            {
                return TryToUnwrap(targetType, source);
            }

            if (ShouldCreateConversionBetweenTypes(targetType, sourceType))
            {
                return TryToCreateMappingExpression(source, targetType, mappingPath, mappingContext, skipCollections);
            }

            return source;
        }

        protected virtual bool ShouldCreateConversionBetweenTypes(ITypeSymbol targetType, ITypeSymbol sourceType)
        {
            return sourceType.CanBeAssignedTo(targetType) == false && ObjectHelper.IsSimpleType(targetType)==false && ObjectHelper.IsSimpleType(sourceType)==false;
        }

        protected virtual MappingElement TryToCreateMappingExpression(MappingElement source, ITypeSymbol targetType,
            MappingPath mappingPath, MappingContext mappingContext, bool skipCollections)
        {
            //TODO: If source expression is method or constructor invocation then we should extract local variable and use it im mappings as a reference
            var namedTargetType = targetType as INamedTypeSymbol;

            if (namedTargetType != null)
            {
                var directlyMappingConstructor = namedTargetType.Constructors.FirstOrDefault(c => c.Parameters.Length == 1 && c.Parameters[0].Type.Equals(source.ExpressionType));
                if (directlyMappingConstructor != null)
                {
                    var constructorParameters = SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(source.Expression));
                    var creationExpression = syntaxGenerator.ObjectCreationExpression(targetType, constructorParameters.Arguments);
                    return new MappingElement()
                    {
                        ExpressionType = targetType,
                        Expression = (ExpressionSyntax) creationExpression
                    };
                }
            }

            if (MappingHelper.IsMappingBetweenCollections(targetType, source.ExpressionType))
            {
                return new MappingElement()
                {
                    ExpressionType = targetType,
                    Expression = MapCollections(source.Expression, source.ExpressionType, targetType, mappingPath.Clone(), mappingContext) as ExpressionSyntax
                };
            }

            var subMappingSourceFinder = new ObjectMembersMappingSourceFinder(source.ExpressionType, source.Expression, syntaxGenerator);

            if (namedTargetType != null)
            {
                //maybe there is constructor that accepts parameter matching source properties
                var constructorOverloadParameterSets = namedTargetType.Constructors.Select(x => x.Parameters);
                var matchedOverload = MethodHelper.FindBestParametersMatch(subMappingSourceFinder, constructorOverloadParameterSets);

                if (matchedOverload != null)
                {
                    var creationExpression = ((ObjectCreationExpressionSyntax)syntaxGenerator.ObjectCreationExpression(targetType, matchedOverload.ToArgumentListSyntax(this).Arguments));
                    var matchedSources = matchedOverload.GetMatchedSources();
                    var restSourceFinder = new IgnorableMappingSourceFinder(subMappingSourceFinder,  foundElement =>
                        {
                            return matchedSources.Any(x => x.Expression.IsEquivalentTo(foundElement.Expression));
                        });
                    var mappingMatcher = new SingleSourceMatcher(restSourceFinder);
                    return new MappingElement()
                    {
                        ExpressionType = targetType,
                        Expression =   AddInitializerWithMapping(creationExpression, mappingMatcher, targetType, mappingContext, skipCollections, mappingPath)
                    };
                }
            }


            var objectCreationExpressionSyntax = ((ObjectCreationExpressionSyntax) syntaxGenerator.ObjectCreationExpression(targetType));
            var subMappingMatcher = new SingleSourceMatcher(subMappingSourceFinder);
            return new MappingElement()
            {
                ExpressionType = targetType,
                Expression = AddInitializerWithMapping(objectCreationExpressionSyntax, subMappingMatcher, targetType, mappingContext, skipCollections, mappingPath)
            };
        }


        public ObjectCreationExpressionSyntax AddInitializerWithMapping(
            ObjectCreationExpressionSyntax objectCreationExpression, IMappingMatcher mappingMatcher,
            ITypeSymbol createdObjectTyp,
            MappingContext mappingContext,
            bool skipCollections = false,
            MappingPath mappingPath = null)
        {
            var propertiesToSet = ObjectHelper.GetFieldsThaCanBeSetPublicly(createdObjectTyp, contextAssembly);
            if (skipCollections)
            {
                propertiesToSet = propertiesToSet.Where(x => ObjectHelper.IsSimpleType(x.Type) || !MappingHelper.IsCollection(x.Type));
            }

            var assignments = MapUsingSimpleAssignment(propertiesToSet, mappingMatcher, mappingContext, skipCollections, mappingPath).ToList();
            if (assignments.Count == 0)
            {
                return objectCreationExpression;
            }
            var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assignments)).FixInitializerExpressionFormatting(objectCreationExpression);
            return objectCreationExpression.WithInitializer(initializerExpressionSyntax);
        }

        public IEnumerable<ExpressionSyntax> MapUsingSimpleAssignment(IEnumerable<IPropertySymbol> targets,
            IMappingMatcher mappingMatcher,
            MappingContext mappingContext,
            bool skipCollections = false,
            MappingPath mappingPath = null, SyntaxNode globalTargetAccessor = null)
        {
            if (mappingPath == null)
            {
                mappingPath = new MappingPath();
            }

            return mappingMatcher.MatchAll(targets, syntaxGenerator, globalTargetAccessor)
                .Select(match =>
                {
                    var sourceMappingElement = this.MapExpression(match.Source, match.Target.ExpressionType, mappingContext, skipCollections, mappingPath.Clone());
                    var sourceExpression = sourceMappingElement.Expression;
                    if (sourceMappingElement.ExpressionType != match.Target.ExpressionType)
                    {
                        mappingContext.AddMissingConversion(sourceMappingElement.ExpressionType, match.Target.ExpressionType);
                        if (mappingContext.WrapInCustomConversion)
                        {
                            var customConversionMethodName = syntaxGenerator.IdentifierName($"MapFrom{sourceMappingElement.ExpressionType.Name}To{match.Target.ExpressionType.Name}");
                            sourceExpression = (ExpressionSyntax) syntaxGenerator.InvocationExpression(customConversionMethodName, sourceExpression);
                        }
                    }

                    return (ExpressionSyntax)syntaxGenerator.AssignmentStatement(match.Target.Expression, sourceExpression);
                }).ToList();
        }

        private bool IsUnwrappingNeeded(ITypeSymbol targetType, MappingElement element)
        {
            return targetType.Equals(element.ExpressionType) == false && (ObjectHelper.IsSimpleType(targetType) || SymbolHelper.IsNullable(targetType, out _));
        }

        private MappingElement TryToUnwrap(ITypeSymbol targetType, MappingElement element)
        {
            var sourceAccess = element.Expression as SyntaxNode;
            var conversion =  semanticModel.Compilation.ClassifyConversion(element.ExpressionType, targetType);
            if (conversion.Exists == false)
            {
                var wrapper = GetWrappingInfo(element.ExpressionType, targetType);
                if (wrapper.Type == WrapperInfoType.Property)
                {
                    return new MappingElement()
                    {
                        Expression = (ExpressionSyntax) syntaxGenerator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingProperty.Name),
                        ExpressionType = wrapper.UnwrappingProperty.Type
                    };
                }
                if (wrapper.Type == WrapperInfoType.Method)
                {
                    var unwrappingMethodAccess = syntaxGenerator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingMethod.Name);

                    return new MappingElement()
                    {
                        Expression = (InvocationExpressionSyntax) syntaxGenerator.InvocationExpression(unwrappingMethodAccess),
                        ExpressionType = wrapper.UnwrappingMethod.ReturnType

                    };
                }

                if (targetType.SpecialType == SpecialType.System_String && element.ExpressionType.TypeKind == TypeKind.Enum)
                {
                    var toStringAccess = syntaxGenerator.MemberAccessExpression(element.Expression, "ToString");
                    return new MappingElement()
                    {
                        Expression = (InvocationExpressionSyntax)syntaxGenerator.InvocationExpression(toStringAccess),
                        ExpressionType = targetType
                    };
                }

                if (element.ExpressionType.SpecialType == SpecialType.System_String && targetType.TypeKind  == TypeKind.Enum)
                {
                    var parseEnumAccess = syntaxGenerator.MemberAccessExpression(SyntaxFactory.ParseTypeName("System.Enum"), "Parse");
                    var enumType = SyntaxFactory.ParseTypeName(targetType.Name);
                    var parseInvocation = (InvocationExpressionSyntax)syntaxGenerator.InvocationExpression(parseEnumAccess, new[]
                    {
                        syntaxGenerator.TypeOfExpression(enumType),
                        element.Expression,
                        syntaxGenerator.TrueLiteralExpression()
                    });

                    return new MappingElement()
                    {
                        Expression = (ExpressionSyntax) syntaxGenerator.CastExpression(enumType, parseInvocation),
                        ExpressionType = targetType
                    };
                }

            }
            else if(conversion.IsExplicit)
            {
                return new MappingElement()
                {
                    Expression = (ExpressionSyntax) syntaxGenerator.CastExpression(targetType, sourceAccess),
                    ExpressionType = targetType
                };
            }
            return element;
        }

        private static WrapperInfo GetWrappingInfo(ITypeSymbol wrapperType, ITypeSymbol wrappedType)
        {
            var unwrappingProperties = ObjectHelper.GetUnwrappingProperties(wrapperType, wrappedType).ToList();
            var unwrappingMethods = ObjectHelper.GetUnwrappingMethods(wrapperType, wrappedType).ToList();
            if (unwrappingMethods.Count + unwrappingProperties.Count == 1)
            {
                if (unwrappingMethods.Count == 1)
                {
                    return new WrapperInfo(unwrappingMethods.First());
                }

                return new WrapperInfo(unwrappingProperties.First());
            }
            return new WrapperInfo();
        }


        private SyntaxNode MapCollections(SyntaxNode sourceAccess, ITypeSymbol sourceListType,
            ITypeSymbol targetListType, MappingPath mappingPath, MappingContext mappingContext)
        {
            var isReadonlyCollection = ObjectHelper.IsReadonlyCollection(targetListType);
            var sourceListElementType = MappingHelper.GetElementType(sourceListType);
            var targetListElementType = MappingHelper.GetElementType(targetListType);
            if (ShouldCreateConversionBetweenTypes(targetListElementType, sourceListElementType))
            {
                var useConvert = CanUseConvert(sourceListType);
                var selectAccess = useConvert ?   syntaxGenerator.MemberAccessExpression(sourceAccess, "ConvertAll"): syntaxGenerator.MemberAccessExpression(sourceAccess, "Select");
                var lambdaParameterName = NameHelper.CreateLambdaParameterName(sourceAccess);
                var mappingLambda = CreateMappingLambda(lambdaParameterName, sourceListElementType, targetListElementType, mappingPath, mappingContext);
                var selectInvocation = syntaxGenerator.InvocationExpression(selectAccess, mappingLambda);
                var toList = useConvert? selectInvocation: AddMaterializeCollectionInvocation(syntaxGenerator, selectInvocation, targetListType);
                return MappingHelper.WrapInReadonlyCollectionIfNecessary(toList, isReadonlyCollection, syntaxGenerator);
            }

            var toListInvocation = AddMaterializeCollectionInvocation(syntaxGenerator, sourceAccess, targetListType);
            return MappingHelper.WrapInReadonlyCollectionIfNecessary(toListInvocation, isReadonlyCollection, syntaxGenerator);
        }

        private bool CanUseConvert(ITypeSymbol sourceListType)
        {
            return sourceListType.Name == "List" && sourceListType.GetMembers("ConvertAll").Length != 0;
        }

	    public SyntaxNode CreateMappingLambda(string lambdaParameterName, ITypeSymbol sourceListElementType,
            ITypeSymbol targetListElementType, MappingPath mappingPath, MappingContext mappingContext)
	    {
            var source = new MappingElement()
            {
                ExpressionType = sourceListElementType,
                Expression = syntaxGenerator.IdentifierName(lambdaParameterName) as ExpressionSyntax
            };
            var listElementMappingStm = MapExpression(source, targetListElementType, mappingContext, false, mappingPath);

		    return syntaxGenerator.ValueReturningLambdaExpression(lambdaParameterName, listElementMappingStm.Expression);
	    }

        private static SyntaxNode AddMaterializeCollectionInvocation(SyntaxGenerator generator, SyntaxNode sourceAccess, ITypeSymbol targetListType)
        {
            var materializeFunction =  GetCollectionMaterializeFunction(targetListType);
            var toListAccess = generator.MemberAccessExpression(sourceAccess, materializeFunction );
            return generator.InvocationExpression(toListAccess);
        }

        private static string GetCollectionMaterializeFunction(ITypeSymbol targetListType)
        {
            var targetTypeName = targetListType.ToDisplayString();
            if (targetTypeName.StartsWith("System.Collections.Immutable.ImmutableArray") || targetTypeName.StartsWith("System.Collections.Immutable.IImmutableArray"))
            {
                return "ToImmutableArray";
            }
            
            if (targetTypeName.StartsWith("System.Collections.Immutable.ImmutableList") || targetTypeName.StartsWith("System.Collections.Immutable.IImmutableList"))
            {
                return "ToImmutableList";
            }
            
            if (targetTypeName.StartsWith("System.Collections.Immutable.ToImmutableSortedSet"))
            {
                return "ToImmutableList";
            }

            if (targetTypeName.StartsWith("System.Collections.Immutable.ImmutableHashSet") || targetTypeName.StartsWith("System.Collections.Immutable.IImmutableSet"))
            {
                return "ToImmutableHashSet";
            }

            return targetListType.Kind == SymbolKind.ArrayType? "ToArray": "ToList";
        }


        public ExpressionSyntax CreateDefaultExpression(ITypeSymbol typeSymbol)
        {
            return (ExpressionSyntax) syntaxGenerator.DefaultExpression(typeSymbol);
        }
    }

    public class MappingPath
    {
        private List<ITypeSymbol> mapped;

        public int Length => mapped.Count;

        private MappingPath(List<ITypeSymbol> mapped)
        {
            this.mapped = mapped;
        }

        public MappingPath()
        {
            this.mapped = new List<ITypeSymbol>();
        }

        public bool AddToMapped(ITypeSymbol newType)
        {
            if (mapped.Contains(newType))
            {
                return false;
            }
            mapped.Add(newType);
            return true;
        }

        public MappingPath Clone()
        {
            return new MappingPath(this.mapped.ToList());
        }
    }
}