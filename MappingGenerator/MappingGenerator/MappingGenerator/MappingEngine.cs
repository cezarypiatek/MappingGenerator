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
        private readonly SemanticModel semanticModel;
        private readonly SyntaxGenerator syntaxGenerator;

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

            if (mappingPath.AddToMapped(element.ExpressionType) == false)
            {
                return new MappingElement()
                {
                    ExpressionType = element.ExpressionType,
                    Expression = element.Expression.WithTrailingTrivia(SyntaxFactory.Comment(" /* Stop recursing mapping */"))
                };
            }

            if (IsUnrappingNeeded(targetType, element))
            {
                return TryToUnwrapp(targetType, element);
            }

            
            if (element.ExpressionType.Equals(targetType) == false && ObjectHelper.IsSimpleType(targetType)==false && ObjectHelper.IsSimpleType(element.ExpressionType)==false)
            {
                return TryToCreateMappingExpression(element, targetType, mappingPath);
            }

            return element;
        }

        private MappingElement TryToCreateMappingExpression(MappingElement source, ITypeSymbol targetType, MappingPath mappingPath)
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
                    var creationExpression = syntaxGenerator.ObjectCreationExpression(targetType, matchedOverload.ToArgumentListSyntax(this).Arguments);
                    return new MappingElement()
                    {
                        ExpressionType = targetType,
                        Expression = (ExpressionSyntax) creationExpression
                    };
                }
            }


            return new MappingElement()
            {
                ExpressionType = targetType,
                Expression = ((ObjectCreationExpressionSyntax) syntaxGenerator.ObjectCreationExpression(targetType)).AddInitializerWithMapping(subMappingSourceFinder, targetType, semanticModel, syntaxGenerator, mappingPath)
            };
        }

        private bool IsUnrappingNeeded(ITypeSymbol targetType, MappingElement element)
        {
            return targetType != element.ExpressionType && ObjectHelper.IsSimpleType(targetType);
        }

        private MappingElement TryToUnwrapp(ITypeSymbol targetType, MappingElement element)
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
            var isReadolyCollection = targetListType.Name == "ReadOnlyCollection";
            var sourceListElementType = MappingHelper.GetElementType(sourceListType);
            var targetListElementType = MappingHelper.GetElementType(targetListType);
            if (ObjectHelper.IsSimpleType(sourceListElementType) || sourceListElementType.Equals(targetListElementType))
            {
                var toListInvocation = AddMaterializeCollectionInvocation(syntaxGenerator, sourceAccess, targetListType);
                return WrapInReadonlyCollectionIfNecessary(isReadolyCollection, toListInvocation, syntaxGenerator);
            }
            var selectAccess = syntaxGenerator.MemberAccessExpression(sourceAccess, "Select");
            var lambdaParameterName = CreateLambdaParameterName(sourceAccess);
            var mappingLambda = CreateMappingLambda(lambdaParameterName, sourceListElementType, targetListElementType, mappingPath);
	        var selectInvocation = syntaxGenerator.InvocationExpression(selectAccess, mappingLambda);
            var toList = AddMaterializeCollectionInvocation(syntaxGenerator, selectInvocation, targetListType);
            return WrapInReadonlyCollectionIfNecessary(isReadolyCollection, toList, syntaxGenerator);
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

	    private static string CreateLambdaParameterName(SyntaxNode sourceList)
        {
            var originalName = sourceList.ToFullString();
            var localVariableName = ToLocalVariableName(originalName);
            var finalName = ToSingularLocalVariableName(localVariableName);
            if (originalName == finalName)
            {
                return $"{finalName}Element";
            }
            return finalName;
        }

        private static char[] FobiddenSigns = new[] {'.', '[', ']', '(', ')'};

        private static string ToLocalVariableName(string proposalLocalName)
        {
            var withoutForbiddenSigns = string.Join("",proposalLocalName.Trim().Split(FobiddenSigns).Where(x=> string.IsNullOrWhiteSpace(x) == false).Select(x=>
            {
                var cleanElement = x.Trim();
                return $"{cleanElement.Substring(0, 1).ToUpper()}{cleanElement.Substring(1)}";
            }));
            return $"{withoutForbiddenSigns.Substring(0, 1).ToLower()}{withoutForbiddenSigns.Substring(1)}";
        }
        
        private static string ToSingularLocalVariableName(string proposalLocalName)
        {
            if (proposalLocalName.EndsWith("s"))
            {
                return proposalLocalName.Substring(0, proposalLocalName.Length - 1);
            }

            return proposalLocalName;
        }

        private static SyntaxNode AddMaterializeCollectionInvocation(SyntaxGenerator generator, SyntaxNode sourceAccess, ITypeSymbol targetListType)
        {
            var materializeFunction =  targetListType.Kind == SymbolKind.ArrayType? "ToArray": "ToList";
            var toListAccess = generator.MemberAccessExpression(sourceAccess, materializeFunction );
            return generator.InvocationExpression(toListAccess);
        }

        private static SyntaxNode WrapInReadonlyCollectionIfNecessary(bool isReadonly, SyntaxNode node, SyntaxGenerator generator)
        {
            if (isReadonly == false)
            {
                return node;
            }

            var accessAsReadonly = generator.MemberAccessExpression(node, "AsReadOnly");
            return generator.InvocationExpression(accessAsReadonly);
        }

        public ExpressionSyntax CreateDefaultExpression(ITypeSymbol typeSymbol)
        {
            return (ExpressionSyntax) syntaxGenerator.DefaultExpression(typeSymbol);
        }
    }

    public class MappingPath
    {
        private List<ITypeSymbol> mapped;

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