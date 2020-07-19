using System;
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

    public class AnnotatedType
    {
        public ITypeSymbol Type { get; }
        public bool CanBeNull { get; }

        public AnnotatedType(ITypeSymbol type)
        {
            Type = type;
            CanBeNull = type.CanBeNull();
        }

        public AnnotatedType(ITypeSymbol type, bool canBeNull)
        {
            Type = type;
            CanBeNull = canBeNull;
        }
    }

    public class MappingEngine
    {
        protected readonly SemanticModel semanticModel;
        protected readonly SyntaxGenerator syntaxGenerator;


        public MappingEngine(SemanticModel semanticModel, SyntaxGenerator syntaxGenerator)
        {
            this.semanticModel = semanticModel;
            this.syntaxGenerator = syntaxGenerator;
        }

        public TypeInfo GetExpressionTypeInfo(SyntaxNode expression)
        {
            return semanticModel.GetTypeInfo(expression);
        }

        public static async Task<MappingEngine> Create(Document document, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            return new MappingEngine(semanticModel, syntaxGenerator);
        }

        public ExpressionSyntax MapExpression(ExpressionSyntax sourceExpression, AnnotatedType sourceType, AnnotatedType destinationType, MappingContext mappingContext)
        {
            var mappingSource = new MappingElement
            {
                Expression = sourceExpression,
                ExpressionType = sourceType
            };
            return MapExpression(mappingSource, destinationType, mappingContext).Expression;
        }

        public MappingElement MapExpression(MappingElement source, AnnotatedType targetType, MappingContext mappingContext, MappingPath mappingPath = null)
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
            if (mappingPath.AddToMapped(sourceType.Type) == false)
            {
                return new MappingElement()
                {
                    ExpressionType = sourceType,
                    Expression = source.Expression.WithTrailingTrivia(SyntaxFactory.Comment(" /* Stop recursive mapping */")),
                };
            }

            if (mappingContext.FindConversion(sourceType.Type, targetType.Type) is {} userDefinedConversion)
            {
                var invocationExpression = (ExpressionSyntax)syntaxGenerator.InvocationExpression(userDefinedConversion, source.Expression);

                if (sourceType.CanBeNull && targetType.CanBeNull)
                {
                    var compareWithNull = syntaxGenerator.ValueNotEqualsExpression(source.Expression, syntaxGenerator.NullLiteralExpression());
                    var expressionText = source.Expression.ToFullString();
                    var throwExpression = syntaxGenerator.ThrowExpression(syntaxGenerator.ObjectCreationExpression(SyntaxFactory.IdentifierName("ArgumentNullException"),new []
                    {
                        syntaxGenerator.LiteralExpression(expressionText),
                        syntaxGenerator.LiteralExpression($"The value of '{expressionText}' cannot be null "),
                    }));
                   
                    return new MappingElement()
                    {
                        ExpressionType = targetType,
                        Expression =  (ExpressionSyntax) syntaxGenerator.ConditionalExpression(compareWithNull, invocationExpression, throwExpression),
                        
                    };
                }

                return new MappingElement()
                {
                    ExpressionType = targetType,
                    Expression = invocationExpression,
                };
            }


            if (ObjectHelper.IsSimpleType(targetType.Type) && SymbolHelper.IsNullable(sourceType.Type, out var underlyingType) )
            {
                //TODO: Handle ut without nullreferance exception possibility
                source = new MappingElement()
                {
                    Expression =  (ExpressionSyntax)syntaxGenerator.MemberAccessExpression(source.Expression, "Value"),
                    ExpressionType = new AnnotatedType(underlyingType, false)
                };
            }

            if (IsUnwrappingNeeded(targetType.Type, source))
            {
                return TryToUnwrap(targetType, source, mappingContext);
            }

            if (ShouldCreateConversionBetweenTypes(targetType.Type, sourceType.Type))
            {
                return TryToCreateMappingExpression(source, targetType, mappingPath, mappingContext);
            }

            return source;
        }

        protected virtual bool ShouldCreateConversionBetweenTypes(ITypeSymbol targetType, ITypeSymbol sourceType)
        {
            return sourceType.CanBeAssignedTo(targetType) == false && ObjectHelper.IsSimpleType(targetType)==false && ObjectHelper.IsSimpleType(sourceType)==false;
        }

        protected virtual MappingElement TryToCreateMappingExpression(MappingElement source, AnnotatedType targetType, MappingPath mappingPath, MappingContext mappingContext)
        {
            //TODO: If source expression is method or constructor invocation then we should extract local variable and use it im mappings as a reference
            var namedTargetType = targetType.Type as INamedTypeSymbol;
            
            if (namedTargetType != null)
            {
                var directlyMappingConstructor = namedTargetType.Constructors.FirstOrDefault(c => c.Parameters.Length == 1 && c.Parameters[0].Type.Equals(source.ExpressionType));
                if (directlyMappingConstructor != null)
                {
                    var constructorParameters = SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(source.Expression));
                    var creationExpression = syntaxGenerator.ObjectCreationExpression(targetType.Type, constructorParameters.Arguments);
                    return new MappingElement()
                    {
                        ExpressionType = targetType,
                        Expression = (ExpressionSyntax) creationExpression,
                    };
                }
            }

            if (MappingHelper.IsMappingBetweenCollections(targetType.Type, source.ExpressionType.Type))
            {
                return new MappingElement()
                {
                    ExpressionType = targetType,
                    Expression = MapCollections(source, targetType, mappingPath.Clone(), mappingContext) as ExpressionSyntax,
                };
            }

            var subMappingSourceFinder = new ObjectMembersMappingSourceFinder(source.ExpressionType, source.Expression, syntaxGenerator);

            if (namedTargetType != null)
            {
                //maybe there is constructor that accepts parameter matching source properties
                var constructorOverloadParameterSets = namedTargetType.Constructors.Select(x => x.Parameters);
                var matchedOverload = MethodHelper.FindBestParametersMatch(subMappingSourceFinder, constructorOverloadParameterSets, mappingContext);

                if (matchedOverload != null)
                {
                    var creationExpression = ((ObjectCreationExpressionSyntax)syntaxGenerator.ObjectCreationExpression(targetType.Type, matchedOverload.ToArgumentListSyntax(this, mappingContext).Arguments));
                    var matchedSources = matchedOverload.GetMatchedSources();
                    var restSourceFinder = new IgnorableMappingSourceFinder(subMappingSourceFinder,  foundElement =>
                        {
                            return matchedSources.Any(x => x.Expression.IsEquivalentTo(foundElement.Expression));
                        });
                    var mappingMatcher = new SingleSourceMatcher(restSourceFinder);
                    return new MappingElement()
                    {
                        ExpressionType = new AnnotatedType(targetType.Type),
                        Expression = AddInitializerWithMapping(creationExpression, mappingMatcher, targetType.Type, mappingContext, mappingPath),
                    };
                }
            }


            var objectCreationExpressionSyntax = ((ObjectCreationExpressionSyntax) syntaxGenerator.ObjectCreationExpression(targetType.Type));
            var subMappingMatcher = new SingleSourceMatcher(subMappingSourceFinder);
            return new MappingElement()
            {
                ExpressionType = new AnnotatedType(targetType.Type),
                Expression = AddInitializerWithMapping(objectCreationExpressionSyntax, subMappingMatcher, targetType.Type, mappingContext, mappingPath),
            };
        }


        public ObjectCreationExpressionSyntax AddInitializerWithMapping(
            ObjectCreationExpressionSyntax objectCreationExpression, IMappingMatcher mappingMatcher,
            ITypeSymbol createdObjectTyp,
            MappingContext mappingContext,
            MappingPath mappingPath = null)
        {
            var propertiesToSet = MappingTargetHelper.GetFieldsThaCanBeSetPublicly(createdObjectTyp, mappingContext);
            var assignments = MapUsingSimpleAssignment(propertiesToSet, mappingMatcher, mappingContext, mappingPath).ToList();
            if (assignments.Count == 0)
            {
                return objectCreationExpression;
            }
            var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assignments)).FixInitializerExpressionFormatting(objectCreationExpression);
            return objectCreationExpression.WithInitializer(initializerExpressionSyntax);
        }

        public IEnumerable<ExpressionSyntax> MapUsingSimpleAssignment(IReadOnlyCollection<IObjectField> targets,
            IMappingMatcher mappingMatcher,
            MappingContext mappingContext,
            MappingPath mappingPath = null, SyntaxNode globalTargetAccessor = null)
        {
            if (mappingPath == null)
            {
                mappingPath = new MappingPath();
            }
          
            return mappingMatcher.MatchAll(targets, syntaxGenerator, mappingContext, globalTargetAccessor)
                .Select(match =>
                {
                    var sourceMappingElement = MapExpression(match.Source,  match.Target.ExpressionType, mappingContext, mappingPath.Clone());
                    var sourceExpression = sourceMappingElement.Expression;
                    if (sourceMappingElement.ExpressionType != match.Target.ExpressionType)
                    {
                        mappingContext.AddMissingConversion(sourceMappingElement.ExpressionType.Type, match.Target.ExpressionType.Type);
                        if (mappingContext.WrapInCustomConversion)
                        {
                            var customConversionMethodName = syntaxGenerator.IdentifierName($"MapFrom{sourceMappingElement.ExpressionType.Type.Name}To{match.Target.ExpressionType.Type.Name}");
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

        private MappingElement TryToUnwrap(AnnotatedType targetType, MappingElement source, MappingContext mappingContext)
        {
            var sourceAccess = source.Expression as SyntaxNode;
            var conversion =  semanticModel.Compilation.ClassifyConversion(source.ExpressionType.Type, targetType.Type);
            if (conversion.Exists == false)
            {
                var wrapper = GetWrappingInfo(source.ExpressionType.Type, targetType.Type, mappingContext);
                if (wrapper.Type == WrapperInfoType.ObjectField)
                {
                    return new MappingElement()
                    {
                        Expression = (ExpressionSyntax) syntaxGenerator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingObjectField.Name),
                        ExpressionType = wrapper.UnwrappingObjectField.Type
                    };
                }
                if (wrapper.Type == WrapperInfoType.Method)
                {
                    var unwrappingMethodAccess = syntaxGenerator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingMethod.Name);
                    
                    return new MappingElement()
                    {
                        Expression = (InvocationExpressionSyntax) syntaxGenerator.InvocationExpression(unwrappingMethodAccess),
                        ExpressionType = new AnnotatedType(wrapper.UnwrappingMethod.ReturnType)
                    };
                }

                if (targetType.Type.SpecialType == SpecialType.System_String && source.ExpressionType.Type.TypeKind == TypeKind.Enum)
                {
                    var toStringAccess = syntaxGenerator.MemberAccessExpression(source.Expression, "ToString");
                    return new MappingElement()
                    {
                        Expression = (InvocationExpressionSyntax)syntaxGenerator.InvocationExpression(toStringAccess),
                        ExpressionType = targetType
                    };
                }

                if (source.ExpressionType.Type.SpecialType == SpecialType.System_String && targetType.Type.TypeKind  == TypeKind.Enum)
                {
                    var parseEnumAccess = syntaxGenerator.MemberAccessExpression(SyntaxFactory.ParseTypeName("System.Enum"), "Parse");
                    var enumType = SyntaxFactory.ParseTypeName(targetType.Type.Name);
                    var parseInvocation = (InvocationExpressionSyntax)syntaxGenerator.InvocationExpression(parseEnumAccess, new[]
                    {
                        syntaxGenerator.TypeOfExpression(enumType),
                        source.Expression,
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
                    Expression = (ExpressionSyntax) syntaxGenerator.CastExpression(targetType.Type, sourceAccess),
                    ExpressionType = targetType
                };
            }
            return source;
        }

        private static WrapperInfo GetWrappingInfo(ITypeSymbol wrapperType, ITypeSymbol wrappedType, MappingContext mappingContext)
        {
            var unwrappingProperties = GetUnwrappingProperties(wrapperType, wrappedType, mappingContext).ToList();
            var unwrappingMethods = GetUnwrappingMethods(wrapperType, wrappedType, mappingContext).ToList();
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

        private static IEnumerable<IMethodSymbol> GetUnwrappingMethods(ITypeSymbol wrapperType, ITypeSymbol wrappedType, MappingContext mappingContext)
        {
            return ObjectHelper.GetWithGetPrefixMethods(wrapperType).Where(x => x.ReturnType == wrappedType && mappingContext.AccessibilityHelper.IsSymbolAccessible(x, wrappedType));
        }

        private static IEnumerable<IObjectField> GetUnwrappingProperties(ITypeSymbol wrapperType, ITypeSymbol wrappedType, MappingContext mappingContext)
        {
            return wrapperType.GetObjectFields().Where(x =>  x.Type == wrappedType && x.CanBeGet(wrappedType, mappingContext));
        }

        private SyntaxNode MapCollections(MappingElement source, AnnotatedType targetListType, MappingPath mappingPath, MappingContext mappingContext)
        {
            var isReadonlyCollection = ObjectHelper.IsReadonlyCollection(targetListType.Type);
            var sourceListElementType = MappingHelper.GetElementType(source.ExpressionType.Type);
            var targetListElementType = MappingHelper.GetElementType(targetListType.Type);
            if (ShouldCreateConversionBetweenTypes(targetListElementType.Type, sourceListElementType.Type))
            {
                var useConvert = CanUseConvert(source.ExpressionType.Type);
                var selectAccess =   SyntaxFactoryExtensions.CreateMemberAccessExpression(source.Expression, source.ExpressionType.CanBeNull, useConvert ? "ConvertAll": "Select");
                var lambdaParameterName = NameHelper.CreateLambdaParameterName(source.Expression);
                var mappingLambda = CreateMappingLambda(lambdaParameterName, sourceListElementType, targetListElementType, mappingPath, mappingContext);
                var selectInvocation = syntaxGenerator.InvocationExpression(selectAccess, mappingLambda);
                var toList = useConvert? selectInvocation: AddMaterializeCollectionInvocation(syntaxGenerator, selectInvocation, targetListType.Type, false);
                return MappingHelper.WrapInReadonlyCollectionIfNecessary(toList, isReadonlyCollection, syntaxGenerator);
            }

            var toListInvocation = AddMaterializeCollectionInvocation(syntaxGenerator, source.Expression, targetListType.Type, source.ExpressionType.CanBeNull);
            return MappingHelper.WrapInReadonlyCollectionIfNecessary(toListInvocation, isReadonlyCollection, syntaxGenerator);
        }

        private bool CanUseConvert(ITypeSymbol sourceListType)
        {
            return sourceListType.Name == "List" && sourceListType.GetMembers("ConvertAll").Length != 0;
        }

	    public SyntaxNode CreateMappingLambda(string lambdaParameterName, AnnotatedType sourceListElementType, AnnotatedType targetListElementType, MappingPath mappingPath, MappingContext mappingContext)
        {
            var source = new MappingElement()
            {
                ExpressionType = sourceListElementType,
                Expression = syntaxGenerator.IdentifierName(lambdaParameterName) as ExpressionSyntax,
                
            };
            var listElementMappingStm = MapExpression(source, targetListElementType, mappingContext, mappingPath);

		    return syntaxGenerator.ValueReturningLambdaExpression(lambdaParameterName, listElementMappingStm.Expression);
	    }

        private static SyntaxNode AddMaterializeCollectionInvocation(SyntaxGenerator generator, SyntaxNode sourceAccess, ITypeSymbol targetListType, bool isSourceNullable)
        {
            var materializeFunction =  GetCollectionMaterializeFunction(targetListType);
            var toListAccess = SyntaxFactoryExtensions.CreateMemberAccessExpression((ExpressionSyntax)sourceAccess, isSourceNullable, materializeFunction);
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
