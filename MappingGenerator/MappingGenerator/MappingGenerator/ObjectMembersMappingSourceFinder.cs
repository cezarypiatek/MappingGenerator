using System;
using System.Collections.Generic;
using System.Linq;
using MappingGenerator.MethodHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public interface IMappingSourceFinder
    {
        MappingElement FindMappingSource(string targetName, ITypeSymbol targetType);
    }

    public class ObjectMembersMappingSourceFinder : IMappingSourceFinder
    {
        private readonly ITypeSymbol sourceType;
        private readonly SyntaxNode sourceGlobalAccessor;
        private readonly SyntaxGenerator generator;
        private readonly SemanticModel semanticModel;
        private readonly Lazy<IReadOnlyList<IPropertySymbol>> sourceProperties;
        private readonly Lazy<IReadOnlyList<IMethodSymbol>> sourceMethods;

        public ObjectMembersMappingSourceFinder(ITypeSymbol sourceType, SyntaxNode sourceGlobalAccessor, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            this.sourceType = sourceType;
            this.sourceGlobalAccessor = sourceGlobalAccessor;
            this.generator = generator;
            this.semanticModel = semanticModel;
            this.sourceProperties = new Lazy<IReadOnlyList<IPropertySymbol>>(() => ObjectHelper.GetPublicPropertySymbols(sourceType)
                .Where(property => property.GetMethod!=null)
                .ToList());
            this.sourceMethods = new Lazy<IReadOnlyList<IMethodSymbol>>(()=>  ObjectHelper.GetPublicGetMethods(sourceType).ToList());
        }

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            var mappingSource = FindSource(targetName);
            if (mappingSource != null && IsUnrappingNeeded(targetType, mappingSource))
            {
                return TryToUnwrapp(mappingSource, targetType);
            }
            return mappingSource;
        } 
        
        public MappingElement FindMappingSource2(string targetName, ITypeSymbol targetType)
        {
            var mappingSource = FindSource(targetName);
            if (mappingSource != null && IsUnrappingNeeded(targetType, mappingSource))
            {
                return TryToUnwrapp(mappingSource, targetType);
            }

            if (mappingSource != null && mappingSource.ExpressionType.Equals(targetType) == false && ObjectHelper.IsSimpleType(targetType)==false && ObjectHelper.IsSimpleType(mappingSource.ExpressionType)==false)
            {
                return TryToCreateMappingExpression(mappingSource, targetType);
            }

            return mappingSource;
        }

        private MappingElement TryToCreateMappingExpression(MappingElement mappingSource, ITypeSymbol targetType)
        {
            if (targetType is INamedTypeSymbol namedTargetType)
            {
                var directlyMappingConstructor = namedTargetType.Constructors.FirstOrDefault(c => c.Parameters.Length == 1 && c.Parameters[0].Type == mappingSource.ExpressionType);
                if (directlyMappingConstructor != null)
                {
                    var constructorParameters = SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(mappingSource.Expression));
                    var creationExpression = generator.ObjectCreationExpression(targetType, constructorParameters.Arguments);
                    return new MappingElement()
                    {
                        ExpressionType = targetType,
                        Expression =  (ExpressionSyntax)creationExpression
                    };
                }
            }
            return mappingSource;
        }




        private static bool IsUnrappingNeeded(ITypeSymbol targetType, MappingElement mappingSource)
        {
            return targetType != mappingSource.ExpressionType && ObjectHelper.IsSimpleType(targetType);
        }

        private MappingElement FindSource(string targetName)
        {
            //Direct 1-1 mapping
            var matchedSourceProperty = sourceProperties.Value.FirstOrDefault(x => x.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
            if (matchedSourceProperty != null)
            {
                return new MappingElement()
                {
                    Expression = (ExpressionSyntax) generator.MemberAccessExpression(sourceGlobalAccessor, matchedSourceProperty.Name),
                    ExpressionType = matchedSourceProperty.Type
                };
            }

            //Non-direct (mapping like y.UserName = x.User.Name)
            var source = FindSubPropertySource(targetName, sourceType, sourceProperties.Value, sourceGlobalAccessor);
            if (source != null)
            {
                return source;
            }

            //Flattening with function eg. t.Total = s.GetTotal()
            var matchedSourceMethod = sourceMethods.Value.FirstOrDefault(x => x.Name.EndsWith(targetName, StringComparison.OrdinalIgnoreCase));
            if (matchedSourceMethod != null)
            {
                var sourceMethodAccessor = generator.MemberAccessExpression(sourceGlobalAccessor, matchedSourceMethod.Name);
                return new MappingElement()
                {
                    Expression = (ExpressionSyntax) generator.InvocationExpression(sourceMethodAccessor),
                    ExpressionType = matchedSourceMethod.ReturnType
                };
            }

            return null;
        }

        private MappingElement FindSubPropertySource(string targetName, ITypeSymbol containingType, IEnumerable<IPropertySymbol> properties, SyntaxNode currentAccessor, string prefix=null)
        {
            if (ObjectHelper.IsSimpleType(containingType))
            {
                return null;
            }

            var subProperty = properties.FirstOrDefault(x=> targetName.StartsWith($"{prefix}{x.Name}", StringComparison.OrdinalIgnoreCase));
            if (subProperty != null)
            {
                var currentNamePart = $"{prefix}{subProperty.Name}";
                var subPropertyAccessor = (ExpressionSyntax) generator.MemberAccessExpression(currentAccessor, subProperty.Name);
                if (targetName.Equals(currentNamePart, StringComparison.OrdinalIgnoreCase))
                {
                    return new MappingElement
                    {
                        Expression = subPropertyAccessor,
                        ExpressionType = subProperty.Type
                    };
                }
                return FindSubPropertySource(targetName, subProperty.Type, ObjectHelper.GetPublicPropertySymbols(subProperty.Type),  subPropertyAccessor, currentNamePart);
            }
            return null;
        }

        private MappingElement TryToUnwrapp(MappingElement mappingSource, ITypeSymbol targetType)
        {
            var sourceAccess = mappingSource.Expression as SyntaxNode;
            var conversion =  semanticModel.Compilation.ClassifyConversion(mappingSource.ExpressionType, targetType);
            if (conversion.Exists == false)
            {
                var wrapper = GetWrappingInfo(mappingSource.ExpressionType, targetType);
                if (wrapper.Type == WrapperInfoType.Property)
                {
                    return new MappingElement()
                    {
                        Expression = (ExpressionSyntax) generator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingProperty.Name),
                        ExpressionType = wrapper.UnwrappingProperty.Type
                    };
                }else if (wrapper.Type == WrapperInfoType.Method)
                {
                    var unwrappingMethodAccess = generator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingMethod.Name);
                    ;
                    return new MappingElement()
                    {
                        Expression = (InvocationExpressionSyntax) generator.InvocationExpression(unwrappingMethodAccess),
                        ExpressionType = wrapper.UnwrappingMethod.ReturnType

                    };
                }

            }else if(conversion.IsExplicit)
            {
                return new MappingElement()
                {
                    Expression = (ExpressionSyntax) generator.CastExpression(targetType, sourceAccess),
                    ExpressionType = targetType
                };
            }
            return mappingSource;
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