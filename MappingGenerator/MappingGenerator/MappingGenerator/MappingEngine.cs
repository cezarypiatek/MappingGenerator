using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.MethodHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
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

        public ExpressionSyntax MapExpression(ExpressionSyntax sourceExpression, ITypeSymbol sourceType, ITypeSymbol destinationType)
        {
            var mappingSource = new MappingElement
            {
                Expression = sourceExpression,
                ExpressionType = sourceType
            };
            return MapExpression(mappingSource, destinationType).Expression;
        }

        public MappingElement MapExpression(MappingElement element, ITypeSymbol targetType, MappingPath mappingPath = null)
        {
            if (element == null)
            {
                return null;
            }

            if (mappingPath == null)
            {
                mappingPath = new MappingPath();
            }

            var sourceType = element.ExpressionType;
            if (mappingPath.AddToMapped(sourceType) == false)
            {
                return new MappingElement()
                {
                    ExpressionType = sourceType,
                    Expression = element.Expression.WithTrailingTrivia(SyntaxFactory.Comment(" /* Stop recursive mapping */"))
                };
            }

            if (ObjectHelper.IsSimpleType(targetType) && SymbolHelper.IsNullable(sourceType, out var underlyingType) )
            {
                element = new MappingElement()
                {
                    Expression =  (ExpressionSyntax)syntaxGenerator.MemberAccessExpression(element.Expression, "Value"),
                    ExpressionType = underlyingType
                };
            }

            if (IsUnwrappingNeeded(targetType, element))
            {
                return TryToUnwrap(targetType, element);
            }

            
            if (ShouldCreateConversionBetweenTypes(targetType, sourceType))
            {
                return TryToCreateMappingExpression(element, targetType, mappingPath);
            }

            return element;
        }

        protected virtual bool ShouldCreateConversionBetweenTypes(ITypeSymbol targetType, ITypeSymbol sourceType)
        {
            return (sourceType.Equals(targetType) == false) && ObjectHelper.IsSimpleType(targetType)==false && ObjectHelper.IsSimpleType(sourceType)==false;
        }

        protected virtual MappingElement TryToCreateMappingExpression(MappingElement source, ITypeSymbol targetType, MappingPath mappingPath)
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
                    Expression = MapCollections(source.Expression, source.ExpressionType, targetType, mappingPath.Clone()) as ExpressionSyntax
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
                    return new MappingElement()
                    {
                        ExpressionType = targetType,
                        Expression =   AddInitializerWithMapping(creationExpression, restSourceFinder, targetType, mappingPath)
                    };
                }
            }


            var objectCreationExpressionSyntax = ((ObjectCreationExpressionSyntax) syntaxGenerator.ObjectCreationExpression(targetType));
            return new MappingElement()
            {
                ExpressionType = targetType,
                Expression = AddInitializerWithMapping(objectCreationExpressionSyntax, subMappingSourceFinder, targetType, mappingPath)
            };
        }


        public ObjectCreationExpressionSyntax AddInitializerWithMapping(
            ObjectCreationExpressionSyntax objectCreationExpression, IMappingSourceFinder mappingSourceFinder,
            ITypeSymbol createdObjectTyp,
            MappingPath mappingPath = null)
        {
            var propertiesToSet = ObjectHelper.GetFieldsThaCanBeSetPublicly(createdObjectTyp, contextAssembly);
            var assignments = MapUsingSimpleAssignment(syntaxGenerator, propertiesToSet, mappingSourceFinder, mappingPath).ToList();
            if (assignments.Count == 0)
            {
                return objectCreationExpression;
            }
            var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assignments)).FixInitializerExpressionFormatting(objectCreationExpression);
            return objectCreationExpression.WithInitializer(initializerExpressionSyntax);
        }

        public IEnumerable<ExpressionSyntax> MapUsingSimpleAssignment(SyntaxGenerator generator,
            IEnumerable<IPropertySymbol> targets, IMappingSourceFinder sourceFinder,
            MappingPath mappingPath = null, SyntaxNode globalTargetAccessor = null)
        {
            if (mappingPath == null)
            {
                mappingPath = new MappingPath();
            }
          
            return targets.Select(property => new
                {
                    source = sourceFinder.FindMappingSource(property.Name, property.Type),
                    target = new MappingElement()
                    {
                        Expression = (ExpressionSyntax)CreateAccessPropertyExpression(globalTargetAccessor, property, generator),
                        ExpressionType = property.Type
                    }
                })
                .Where(x => x.source != null)
                .Select(pair =>
                {
                    var sourceExpression = this.MapExpression(pair.source, pair.target.ExpressionType, mappingPath.Clone()).Expression;
                    return (ExpressionSyntax)generator.AssignmentStatement(pair.target.Expression, sourceExpression);
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


        private bool IsUnwrappingNeeded(ITypeSymbol targetType, MappingElement element)
        {
            return targetType.Equals(element.ExpressionType) == false && ObjectHelper.IsSimpleType(targetType);
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
                }else if (wrapper.Type == WrapperInfoType.Method)
                {
                    var unwrappingMethodAccess = syntaxGenerator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingMethod.Name);
                    
                    return new MappingElement()
                    {
                        Expression = (InvocationExpressionSyntax) syntaxGenerator.InvocationExpression(unwrappingMethodAccess),
                        ExpressionType = wrapper.UnwrappingMethod.ReturnType

                    };
                }

            }else if(conversion.IsExplicit)
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


        private SyntaxNode MapCollections(SyntaxNode sourceAccess, ITypeSymbol sourceListType, ITypeSymbol targetListType, MappingPath mappingPath)
        {
            var isReadonlyCollection = ObjectHelper.IsReadonlyCollection(targetListType);
            var sourceListElementType = MappingHelper.GetElementType(sourceListType);
            var targetListElementType = MappingHelper.GetElementType(targetListType);
            if (ShouldCreateConversionBetweenTypes(targetListElementType, sourceListElementType))
            {
                var useConvert = CanUseConvert(sourceListType);
                var selectAccess = useConvert ?   syntaxGenerator.MemberAccessExpression(sourceAccess, "ConvertAll"): syntaxGenerator.MemberAccessExpression(sourceAccess, "Select");
                var lambdaParameterName = NameHelper.CreateLambdaParameterName(sourceAccess);
                var mappingLambda = CreateMappingLambda(lambdaParameterName, sourceListElementType, targetListElementType, mappingPath);
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

	    public SyntaxNode CreateMappingLambda(string lambdaParameterName, ITypeSymbol sourceListElementType, ITypeSymbol targetListElementType, MappingPath mappingPath)
	    {
		    var listElementMappingStm = MapExpression(new MappingElement()
			    {
				    ExpressionType = sourceListElementType,
				    Expression = syntaxGenerator.IdentifierName(lambdaParameterName) as ExpressionSyntax
			    },
			    targetListElementType, mappingPath);

		    return syntaxGenerator.ValueReturningLambdaExpression(lambdaParameterName, listElementMappingStm.Expression);
	    }

        private static SyntaxNode AddMaterializeCollectionInvocation(SyntaxGenerator generator, SyntaxNode sourceAccess, ITypeSymbol targetListType)
        {
            var materializeFunction =  targetListType.Kind == SymbolKind.ArrayType? "ToArray": "ToList";
            var toListAccess = generator.MemberAccessExpression(sourceAccess, materializeFunction );
            return generator.InvocationExpression(toListAccess);
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