using System.Linq;
using MappingGenerator.MethodHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public class MappingElement
    {
        private readonly SyntaxGenerator generator;
        private readonly SemanticModel semanticModel;
        public ExpressionSyntax Expression { get; set; }
        public ITypeSymbol ExpressionType { get; set; }

        public MappingElement(SyntaxGenerator generator, SemanticModel semanticModel)
        {
            this.generator = generator;
            this.semanticModel = semanticModel;
        }

        public MappingElement AdjustToType(ITypeSymbol targetType)
        {
            if (IsUnrappingNeeded(targetType))
            {
                return TryToUnwrapp(targetType);
            }

            
            if (this.ExpressionType.Equals(targetType) == false && ObjectHelper.IsSimpleType(targetType)==false && ObjectHelper.IsSimpleType(this.ExpressionType)==false)
            {
                return TryToCreateMappingExpression(this, targetType);
            }

            return this;
        }

        private  MappingElement TryToCreateMappingExpression(MappingElement source, ITypeSymbol targetType)
        {
            if (targetType is INamedTypeSymbol namedTargetType)
            {

                var directlyMappingConstructor = namedTargetType.Constructors.FirstOrDefault(c => c.Parameters.Length == 1 && c.Parameters[0].Type.Equals(source.ExpressionType));
                if (directlyMappingConstructor != null)
                {
                    var constructorParameters = SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(source.Expression));
                    var creationExpression = generator.ObjectCreationExpression(targetType, constructorParameters.Arguments);
                    return new MappingElement(generator, semanticModel)
                    {
                        ExpressionType = targetType,
                        Expression =  (ExpressionSyntax)creationExpression
                    };
                }

                if (MappingHelper.IsMappingBetweenCollections(targetType, source.ExpressionType))
                {
                    return new MappingElement(generator, semanticModel)
                    {
                        ExpressionType = targetType,
                        Expression = MapCollections(source.Expression, source.ExpressionType, targetType) as ExpressionSyntax
                    };
                }

                var subMappingSourceFinder = new ObjectMembersMappingSourceFinder(source.ExpressionType, source.Expression, generator, semanticModel);

                //maybe there is constructor that accepts parameter matching source properties
                var constructorOverloadParameterSets = namedTargetType.Constructors.Select(x=>x.Parameters);
                var matchedOverload =  MethodHelper.FindBestParametersMatch(subMappingSourceFinder, constructorOverloadParameterSets);

                if (matchedOverload != null)
                {
                    var creationExpression = generator.ObjectCreationExpression(targetType, matchedOverload.ToArgumentListSyntax(generator).Arguments);
                    return new MappingElement(generator, semanticModel)
                    {
                        ExpressionType = targetType,
                        Expression =  (ExpressionSyntax)creationExpression
                    };
                }
                
                return new MappingElement(generator, semanticModel)
                {
                    ExpressionType = targetType,
                    Expression = MappingHelper.CreateObjectCreationExpressionWithInitializer(targetType, subMappingSourceFinder, generator, semanticModel)
                };
            }
            return this;
        }

        private bool IsUnrappingNeeded(ITypeSymbol targetType)
        {
            return targetType != this.ExpressionType && ObjectHelper.IsSimpleType(targetType);
        }

        private MappingElement TryToUnwrapp(ITypeSymbol targetType)
        {
            var sourceAccess = this.Expression as SyntaxNode;
            var conversion =  semanticModel.Compilation.ClassifyConversion(this.ExpressionType, targetType);
            if (conversion.Exists == false)
            {
                var wrapper = GetWrappingInfo(this.ExpressionType, targetType);
                if (wrapper.Type == WrapperInfoType.Property)
                {
                    return new MappingElement(generator, semanticModel)
                    {
                        Expression = (ExpressionSyntax) generator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingProperty.Name),
                        ExpressionType = wrapper.UnwrappingProperty.Type
                    };
                }else if (wrapper.Type == WrapperInfoType.Method)
                {
                    var unwrappingMethodAccess = generator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingMethod.Name);
                    
                    return new MappingElement(generator, semanticModel)
                    {
                        Expression = (InvocationExpressionSyntax) generator.InvocationExpression(unwrappingMethodAccess),
                        ExpressionType = wrapper.UnwrappingMethod.ReturnType

                    };
                }

            }else if(conversion.IsExplicit)
            {
                return new MappingElement(generator, semanticModel)
                {
                    Expression = (ExpressionSyntax) generator.CastExpression(targetType, sourceAccess),
                    ExpressionType = targetType
                };
            }
            return this;
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


        private SyntaxNode MapCollections(SyntaxNode sourceAccess, ITypeSymbol sourceListType, ITypeSymbol targetListType)
        {
            
            var isReadolyCollection = targetListType.Name == "ReadOnlyCollection";
            var sourceListElementType = MappingHelper.GetElementType(sourceListType);
            var targetListElementType = MappingHelper.GetElementType(targetListType);
            if (ObjectHelper.IsSimpleType(sourceListElementType) || sourceListElementType.Equals(targetListElementType))
            {
                var toListInvocation = AddMaterializeCollectionInvocation(generator, sourceAccess, targetListType);
                return WrapInReadonlyCollectionIfNecessary(isReadolyCollection, toListInvocation, generator);
            }
            var selectAccess = generator.MemberAccessExpression(sourceAccess, "Select");
            var lambdaParameterName = CreateLambdaParameterName(sourceAccess);
            var listElementMappingStm = TryToCreateMappingExpression(new MappingElement(generator, semanticModel)
                {
                    ExpressionType = sourceListElementType,
                    Expression = generator.IdentifierName(lambdaParameterName) as ExpressionSyntax
                },
                targetListElementType);
            
            var selectInvocation = generator.InvocationExpression(selectAccess, generator.ValueReturningLambdaExpression(lambdaParameterName,listElementMappingStm.Expression));
            var toList = AddMaterializeCollectionInvocation(generator, selectInvocation, targetListType);
            return WrapInReadonlyCollectionIfNecessary(isReadolyCollection, toList, generator);
        }

        private static string CreateLambdaParameterName(SyntaxNode sourceList)
        {
            var localVariableName = ToLocalVariableName(sourceList.ToFullString());
            return ToSingularLocalVariableName(localVariableName);
        }

        private static char[] FobiddenSigns = new[] {'.', '[', ']', '(', ')'};

        private static string ToLocalVariableName(string proposalLocalName)
        {
            var withoutForbiddenSigns = string.Join("",proposalLocalName.Trim().Split(FobiddenSigns).Select(x=>
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
    }
}