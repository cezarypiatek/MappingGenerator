using System.Linq;
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
                return TryToCreateMappingExpression(targetType);
            }

            return this;
        }

        private MappingElement TryToCreateMappingExpression(ITypeSymbol targetType)
        {
            if (targetType is INamedTypeSymbol namedTargetType)
            {
                var directlyMappingConstructor = namedTargetType.Constructors.FirstOrDefault(c => c.Parameters.Length == 1 && c.Parameters[0].Type == this.ExpressionType);
                if (directlyMappingConstructor != null)
                {
                    var constructorParameters = SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(this.Expression));
                    var creationExpression = generator.ObjectCreationExpression(targetType, constructorParameters.Arguments);
                    return new MappingElement(generator, semanticModel)
                    {
                        ExpressionType = targetType,
                        Expression =  (ExpressionSyntax)creationExpression
                    };
                }

                //maybe this is collection-to-collection mapping
                //maybe there is constructor that accepts parameter matching source properties
               
                if (MappingGenerator.IsMappingBetweenCollections(targetType, this.ExpressionType) == false)
                {
                    // map property by property
                    var subMappingSourceFinder = new ObjectMembersMappingSourceFinder(this.ExpressionType, this.Expression, generator, semanticModel);
                    var propertiesToSet = ObjectHelper.GetPublicPropertySymbols(targetType).Where(x => x.SetMethod?.DeclaredAccessibility == Accessibility.Public);
                    var assigments =  propertiesToSet.Select(x =>
                    {
                        var src = subMappingSourceFinder.FindMappingSource(x.Name, x.Type);
                        if (src != null)
                        {
                            return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(x.Name), src.Expression);
                        }

                        return null;
                    }).OfType<ExpressionSyntax>();
                    
                    return new MappingElement(generator, semanticModel)
                    {
                        ExpressionType = targetType,
                        Expression = ((ObjectCreationExpressionSyntax)generator.ObjectCreationExpression(targetType)).WithInitializer( SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression,new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assigments)))
                    };
                }
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
                    ;
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
    }
}