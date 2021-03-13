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
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
namespace MappingGenerator.Mappings
{
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
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            return new MappingEngine(semanticModel, syntaxGenerator);
        }

        public async Task<ExpressionSyntax> MapExpression(ExpressionSyntax sourceExpression, AnnotatedType sourceType, AnnotatedType destinationType, MappingContext mappingContext)
        {
            var mappingSource = new MappingElement
            {
                Expression = sourceExpression,
                ExpressionType = sourceType
            };
            var mappingElement = await MapExpression(mappingSource, destinationType, mappingContext).ConfigureAwait(false);
            return mappingElement.Expression;
        }

        public async Task<MappingElement> MapExpression(MappingElement source, AnnotatedType targetType, MappingContext mappingContext, MappingPath mappingPath = null)
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

            if (mappingContext.FindConversion(sourceType, targetType) is {} userDefinedConversion)
            {
                //TODO: Check if conversion accept nullable type
                var invocationExpression = (ExpressionSyntax)syntaxGenerator.InvocationExpression(userDefinedConversion.Conversion, source.Expression);
                var protectResultFromNull = targetType.CanBeNull == false && userDefinedConversion.ToType.CanBeNull;
                var conversionExpression = protectResultFromNull ? OrFailWhenExpressionNull(invocationExpression) : invocationExpression;
                return new MappingElement
                {
                    ExpressionType = targetType,
                    Expression = HandleSafeNull(source, userDefinedConversion.FromType, conversionExpression)
                };
            }


            if (ObjectHelper.IsSimpleType(targetType.Type) && SymbolHelper.IsNullable(sourceType.Type, out var underlyingType))
            {
                var mapping = new MappingElement
                {
                    Expression = OrFailWhenArgumentNull(source.Expression),
                    ExpressionType = new AnnotatedType(underlyingType, false)
                };

                return IsConversionToSimpleTypeNeeded(targetType.Type, underlyingType) ? ConvertToSimpleType(targetType, mapping, mappingContext) : mapping;
            }

            if (IsConversionToSimpleTypeNeeded(targetType.Type, source.ExpressionType.Type))
            {
                var conversion = ConvertToSimpleType(targetType, source, mappingContext);
                if (targetType.CanBeNull == false && conversion.ExpressionType.CanBeNull)
                {
                    return new MappingElement
                    {
                        ExpressionType = conversion.ExpressionType.AsNotNull(),
                        Expression = OrFailWhenArgumentNull(conversion.Expression)
                    };
                }
                return conversion;
            }

            if (ShouldCreateConversionBetweenTypes(targetType.Type, sourceType.Type))
            {
                return await  TryToCreateMappingExpression(source, targetType, mappingPath, mappingContext).ConfigureAwait(false);
            }


            if (source.ExpressionType.Type.Equals(targetType.Type) && source.ExpressionType.CanBeNull && targetType.CanBeNull == false)
            {
                return new MappingElement
                {
                    Expression = OrFailWhenArgumentNull(source.Expression),
                    ExpressionType = source.ExpressionType.AsNotNull()
                };
            }
            return source;
        }

        private static BinaryExpressionSyntax OrFailWhenArgumentNull(ExpressionSyntax expression, string messageExpression = null)
        {
            return BinaryExpression(SyntaxKind.CoalesceExpression, expression, ThrowNullArgumentException(messageExpression ?? expression.ToFullString()));
        }
       private static BinaryExpressionSyntax OrFailWhenExpressionNull(ExpressionSyntax expression)
       {
            return BinaryExpression(SyntaxKind.CoalesceExpression, expression, ThrowNullReferenceException(expression.ToFullString()));
       }

        protected virtual bool ShouldCreateConversionBetweenTypes(ITypeSymbol targetType, ITypeSymbol sourceType)
        {
            return sourceType.CanBeAssignedTo(targetType) == false && ObjectHelper.IsSimpleType(targetType)==false && ObjectHelper.IsSimpleType(sourceType)==false;
        }

        protected virtual async Task<MappingElement> TryToCreateMappingExpression(MappingElement source, AnnotatedType targetType, MappingPath mappingPath, MappingContext mappingContext)
        {
            //TODO: If source expression is method or constructor invocation then we should extract local variable and use it im mappings as a reference
            var namedTargetType = targetType.Type as INamedTypeSymbol;
            
            if (namedTargetType != null)
            {
                var directlyMappingConstructor = namedTargetType.Constructors.FirstOrDefault(c => c.Parameters.Length == 1 && c.Parameters[0].Type.Equals(source.ExpressionType.Type));
                if (directlyMappingConstructor != null)
                {
                    var creationExpression = CreateObject(targetType.Type, SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(source.Expression)));
                    var shouldProtectAgainstNull = directlyMappingConstructor.Parameters[0].Type.CanBeNull() == false && source.ExpressionType.CanBeNull;
                    return new MappingElement
                    {
                        ExpressionType = targetType,
                        Expression = shouldProtectAgainstNull ? HandleSafeNull(source, targetType, creationExpression) : creationExpression
                    };
                }
            }

            if (MappingHelper.IsMappingBetweenCollections(targetType.Type, source.ExpressionType.Type))
            {
                var shouldProtectAgainstNull = source.ExpressionType.CanBeNull && targetType.CanBeNull == false;
                var collectionMapping = (await MapCollectionsAsync(source, targetType, mappingPath.Clone(), mappingContext).ConfigureAwait(false)) as ExpressionSyntax;
                return new MappingElement
                {
                    ExpressionType = targetType,
                    Expression = shouldProtectAgainstNull ? OrFailWhenArgumentNull(collectionMapping, source.Expression.ToFullString()) : collectionMapping,
                };
            }

            var subMappingSourceFinder = new ObjectMembersMappingSourceFinder(source.ExpressionType.AsNotNull(), source.Expression);

            if (namedTargetType != null)
            {
                //maybe there is constructor that accepts parameter matching source properties
                var constructorOverloadParameterSets = namedTargetType.Constructors.Select(x => x.Parameters);
                var matchedOverload = await MethodHelper.FindBestParametersMatch(subMappingSourceFinder, constructorOverloadParameterSets, mappingContext).ConfigureAwait(false);

                if (matchedOverload != null)
                {
                    var argumentListSyntaxAsync = await matchedOverload.ToArgumentListSyntaxAsync(this, mappingContext).ConfigureAwait(false);
                    var creationExpression = CreateObject(targetType.Type, argumentListSyntaxAsync);
                    var matchedSources = matchedOverload.GetMatchedSources();
                    var restSourceFinder = new IgnorableMappingSourceFinder(subMappingSourceFinder,  foundElement =>
                        {
                            return matchedSources.Any(x => x.Expression.IsEquivalentTo(foundElement.Expression));
                        });
                    var mappingMatcher = new SingleSourceMatcher(restSourceFinder);
                    return new MappingElement()
                    {
                        ExpressionType = new AnnotatedType(targetType.Type),
                        Expression = await AddInitializerWithMappingAsync(creationExpression, mappingMatcher, targetType.Type, mappingContext, mappingPath).ConfigureAwait(false),
                    };
                }
            }

            var objectCreationExpressionSyntax = CreateObject(targetType.Type);
            var subMappingMatcher = new SingleSourceMatcher(subMappingSourceFinder);
            var objectCreationWithInitializer = await AddInitializerWithMappingAsync(objectCreationExpressionSyntax, subMappingMatcher, targetType.Type, mappingContext, mappingPath).ConfigureAwait(false);
            return new MappingElement()
            {
                ExpressionType = new AnnotatedType(targetType.Type),
                Expression = HandleSafeNull(source, targetType, objectCreationWithInitializer)
            };
        }

        private ExpressionSyntax HandleSafeNull(MappingElement source, AnnotatedType targetType, ExpressionSyntax expression)
        {
            if (source.ExpressionType.CanBeNull)
            {
                var condition = BinaryExpression(SyntaxKind.NotEqualsExpression, source.Expression, LiteralExpression(SyntaxKind.NullLiteralExpression));
                var whenNull = targetType.CanBeNull ? (ExpressionSyntax) LiteralExpression(SyntaxKind.NullLiteralExpression) : ThrowNullArgumentException(source.Expression.ToFullString());
                return ConditionalExpression(condition, expression, whenNull);
            }
            return expression;
        }

        private static ThrowExpressionSyntax ThrowNullArgumentException(string expressionText)
        {
            var methodArgumentName = (expressionText.Contains(".")?  expressionText.Substring(0, expressionText.IndexOf('.')): expressionText).TrimEnd('?');
            var nameofInvocation=  InvocationExpression(IdentifierName("nameof")).WithArgumentList(ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(IdentifierName(methodArgumentName)))));
            var errorMessageExpression = LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal($"The value of '{expressionText}' should not be null"));
            var exceptionTypeName = SyntaxFactory.IdentifierName("ArgumentNullException");
            var exceptionParameters = ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>().AddRange(new []{ Argument(nameofInvocation) , Argument(errorMessageExpression)}));
            var throwExpressionSyntax = SyntaxFactory.ThrowExpression(CreateObject(exceptionTypeName, exceptionParameters));
            return throwExpressionSyntax;
        }
        
        private static ThrowExpressionSyntax ThrowNullReferenceException(string expressionText)
        {
            var errorMessageExpression = LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal($"The value of '{expressionText}' should not be null"));
            var exceptionTypeName = SyntaxFactory.IdentifierName("NullReferenceException");
            var exceptionParameters = ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>().AddRange(new []{ Argument(errorMessageExpression)}));
            var throwExpressionSyntax = SyntaxFactory.ThrowExpression(CreateObject(exceptionTypeName, exceptionParameters));
            return throwExpressionSyntax;
        }

        private ObjectCreationExpressionSyntax CreateObject(ITypeSymbol type, ArgumentListSyntax argumentList = null)
        {
            var typeWithoutNullable = type.StripNullability();
            var identifierNameSyntax = (TypeSyntax) syntaxGenerator.TypeExpression(typeWithoutNullable);
            return CreateObject(identifierNameSyntax, argumentList);
        }

        private static ObjectCreationExpressionSyntax CreateObject(TypeSyntax identifierNameSyntax, ArgumentListSyntax argumentList)
        {
            return ObjectCreationExpression(identifierNameSyntax).WithArgumentList(argumentList ?? ArgumentList());
        }

        private readonly MappingTargetHelper _mappingTargetHelper = new MappingTargetHelper();
        public async Task<ObjectCreationExpressionSyntax> AddInitializerWithMappingAsync(
            ObjectCreationExpressionSyntax objectCreationExpression, IMappingMatcher mappingMatcher,
            ITypeSymbol createdObjectTyp,
            MappingContext mappingContext,
            MappingPath mappingPath = null)
        {
            var propertiesToSet = _mappingTargetHelper.GetFieldsThaCanBeSetPublicly(createdObjectTyp, mappingContext);
            var assignments = await MapUsingSimpleAssignment(propertiesToSet, mappingMatcher, mappingContext, mappingPath).ConfigureAwait(false);
            return SyntaxFactoryExtensions.WithMembersInitialization(objectCreationExpression, assignments);
        }



        public async Task<IReadOnlyList<AssignmentExpressionSyntax>> MapUsingSimpleAssignment(IReadOnlyCollection<IObjectField> targets,
            IMappingMatcher mappingMatcher,
            MappingContext mappingContext,
            MappingPath mappingPath = null, SyntaxNode globalTargetAccessor = null)
        {
            if (mappingPath == null)
            {
                mappingPath = new MappingPath();
            }

            var results = new List<AssignmentExpressionSyntax>();
            foreach (var match in await mappingMatcher.MatchAll(targets, syntaxGenerator, mappingContext, globalTargetAccessor).ConfigureAwait(false))
            {
                var sourceMappingElement = await MapExpression(match.Source, match.Target.ExpressionType, mappingContext, mappingPath.Clone()).ConfigureAwait(false);
                var sourceExpression = sourceMappingElement.Expression;
                if (sourceMappingElement.ExpressionType != match.Target.ExpressionType)
                {
                    mappingContext.AddMissingConversion(sourceMappingElement.ExpressionType.Type, match.Target.ExpressionType.Type);
                    if (mappingContext.WrapInCustomConversion)
                    {
                        var customConversionMethodName = syntaxGenerator.IdentifierName($"MapFrom{sourceMappingElement.ExpressionType.Type.Name}To{match.Target.ExpressionType.Type.Name}");
                        sourceExpression = (ExpressionSyntax)syntaxGenerator.InvocationExpression(customConversionMethodName, sourceExpression);
                    }
                }

                var assignmentExpression = AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, match.Target.Expression, sourceExpression);
                results.Add(assignmentExpression);
            }

            return results;
        }

        private bool IsConversionToSimpleTypeNeeded(ITypeSymbol targetType, ITypeSymbol sourceType)
        {
            return targetType.Equals(sourceType) == false && (ObjectHelper.IsSimpleType(targetType) || SymbolHelper.IsNullable(targetType, out _));
        }

        private MappingElement ConvertToSimpleType(AnnotatedType targetType, MappingElement source, MappingContext mappingContext)
        {
            var conversion = semanticModel.Compilation.ClassifyConversion(source.ExpressionType.Type, targetType.Type);
            if (conversion.Exists == false)
            {
                var wrapper = GetWrappingInfo(source.ExpressionType.Type, targetType.Type, mappingContext);
                if (wrapper.Type == WrapperInfoType.ObjectField)
                {
                    return new MappingElement
                    {
                        Expression = SyntaxFactoryExtensions.CreateMemberAccessExpression(source.Expression, source.ExpressionType.CanBeNull, wrapper.UnwrappingObjectField.Name),
                        ExpressionType = wrapper.UnwrappingObjectField.Type
                    };
                }
                if (wrapper.Type == WrapperInfoType.Method)
                {
                    return new MappingElement
                    {
                        Expression = SyntaxFactoryExtensions.CreateMethodAccessExpression(source.Expression, source.ExpressionType.CanBeNull, wrapper.UnwrappingMethod.Name),
                        ExpressionType = new AnnotatedType(wrapper.UnwrappingMethod.ReturnType)
                    };
                }

                if (targetType.Type.SpecialType == SpecialType.System_String && source.ExpressionType.Type.TypeKind == TypeKind.Enum)
                {
                    var toStringAccess = SyntaxFactoryExtensions.CreateMethodAccessExpression(source.Expression, source.ExpressionType.CanBeNull,"ToString");
                    return new MappingElement
                    {
                        Expression = toStringAccess,
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
                        ExpressionType = targetType.AsNotNull()
                    };
                }

            }
            else if(conversion.IsExplicit)
            {
                return new MappingElement()
                {
                    Expression = (ExpressionSyntax) syntaxGenerator.CastExpression(targetType.Type, source.Expression),
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
            return ObjectHelper.GetWithGetPrefixMethods(wrapperType).Where(x => x.ReturnType.Equals(wrappedType) && mappingContext.AccessibilityHelper.IsSymbolAccessible(x, wrappedType));
        }

        private static IEnumerable<IObjectField> GetUnwrappingProperties(ITypeSymbol wrapperType, ITypeSymbol wrappedType, MappingContext mappingContext)
        {
            return wrapperType.GetObjectFields().Where(x =>  x.Type.Type.Equals(wrappedType) && x.CanBeGet(wrappedType, mappingContext));
        }

        private async Task<SyntaxNode> MapCollectionsAsync(MappingElement source, AnnotatedType targetListType, MappingPath mappingPath, MappingContext mappingContext)
        {
            var isReadonlyCollection = ObjectHelper.IsReadonlyCollection(targetListType.Type);
            var sourceListElementType = MappingHelper.GetElementType(source.ExpressionType.Type);
            var targetListElementType = MappingHelper.GetElementType(targetListType.Type);
            if (sourceListElementType.CanBeNull && targetListElementType.CanBeNull == false)
            {
                var whereFilter = SyntaxFactoryExtensions.CreateMethodAccessExpression(source.Expression, source.ExpressionType.CanBeNull, $"OfType<{sourceListElementType.Type.Name}>");
                var lambdaParameterName = NameHelper.CreateLambdaParameterName(source.Expression);
                var mappingLambda = await CreateMappingLambdaAsync(lambdaParameterName, sourceListElementType.AsNotNull(), targetListElementType, mappingPath, mappingContext).ConfigureAwait(false);
                var selectExpression = SyntaxFactoryExtensions.CreateMethodAccessExpression(whereFilter, false, "Select", mappingLambda);
                var toList = AddMaterializeCollectionInvocation(syntaxGenerator, selectExpression, targetListType.Type, false);
                return MappingHelper.WrapInReadonlyCollectionIfNecessary(toList, isReadonlyCollection, syntaxGenerator);
            }

            if (ShouldCreateConversionBetweenTypes(targetListElementType.Type, sourceListElementType.Type))
            {
                var useConvert = CanUseConvert(source.ExpressionType.Type);
                var mapMethod = useConvert ? "ConvertAll": "Select";
                var lambdaParameterName = NameHelper.CreateLambdaParameterName(source.Expression);
                var mappingLambda = await CreateMappingLambdaAsync(lambdaParameterName, sourceListElementType, targetListElementType, mappingPath, mappingContext).ConfigureAwait(false);
                var selectExpression =   SyntaxFactoryExtensions.CreateMethodAccessExpression(source.Expression, source.ExpressionType.CanBeNull, mapMethod, mappingLambda);
                var toList = useConvert? selectExpression: AddMaterializeCollectionInvocation(syntaxGenerator, selectExpression, targetListType.Type, false);
                return MappingHelper.WrapInReadonlyCollectionIfNecessary(toList, isReadonlyCollection, syntaxGenerator);
            }

            var toListInvocation = AddMaterializeCollectionInvocation(syntaxGenerator, source.Expression, targetListType.Type, source.ExpressionType.CanBeNull);
            return MappingHelper.WrapInReadonlyCollectionIfNecessary(toListInvocation, isReadonlyCollection, syntaxGenerator);
        }

        private bool CanUseConvert(ITypeSymbol sourceListType)
        {
            return sourceListType.Name == "List" && sourceListType.GetMembers("ConvertAll").Length != 0;
        }

	    public async Task<ExpressionSyntax> CreateMappingLambdaAsync(string lambdaParameterName, AnnotatedType sourceListElementType, AnnotatedType targetListElementType, MappingPath mappingPath, MappingContext mappingContext)
        {
            var source = new MappingElement()
            {
                ExpressionType = sourceListElementType,
                Expression = syntaxGenerator.IdentifierName(lambdaParameterName) as ExpressionSyntax,
                
            };
            var listElementMappingStm = await MapExpression(source, targetListElementType, mappingContext, mappingPath).ConfigureAwait(false);

		    return (ExpressionSyntax) syntaxGenerator.ValueReturningLambdaExpression(lambdaParameterName, listElementMappingStm.Expression);
	    }

        private static SyntaxNode AddMaterializeCollectionInvocation(SyntaxGenerator generator, SyntaxNode sourceAccess, ITypeSymbol targetListType, bool isSourceNullable)
        {
            if (targetListType.TypeKind == TypeKind.Interface && targetListType.Name == "IEnumerable")
            {
                return sourceAccess;
            }

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
