using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public class MappingSourceFinder
    {
        private readonly SyntaxNode sourceGlobalAccessor;
        private readonly SyntaxGenerator generator;
        private readonly SemanticModel semanticModel;
        private readonly Lazy<IReadOnlyList<IPropertySymbol>> sourceProperties;
        private readonly Lazy<IReadOnlyList<IMethodSymbol>> sourceMethods;

        public MappingSourceFinder(ITypeSymbol sourceType, SyntaxNode sourceGlobalAccessor, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            this.sourceGlobalAccessor = sourceGlobalAccessor;
            this.generator = generator;
            this.semanticModel = semanticModel;
            this.sourceProperties = new Lazy<IReadOnlyList<IPropertySymbol>>(() => ObjectHelper.GetPublicPropertySymbols(sourceType).ToList());
            this.sourceMethods = new Lazy<IReadOnlyList<IMethodSymbol>>(()=>  ObjectHelper.GetPublicGetMethods(sourceType).ToList());
        }

        public MappingSource FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            var mappingSource = FindSource(targetName);
            if (mappingSource != null && IsUnrappingNeeded(targetType, mappingSource))
            {
                return TryToUnwrapp(mappingSource, targetType);
            }
            return mappingSource;
        }

        private static bool IsUnrappingNeeded(ITypeSymbol targetType, MappingSource mappingSource)
        {
            return targetType != mappingSource.ExpressionType && ObjectHelper.IsSimpleType(targetType);
        }

        private MappingSource FindSource(string targetName)
        {
            //Direct 1-1 mapping
            var matchedSourceProperty =
                sourceProperties.Value.FirstOrDefault(x => x.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
            if (matchedSourceProperty != null)
            {
                return new MappingSource()
                {
                    Expression =
                        (ExpressionSyntax) generator.MemberAccessExpression(sourceGlobalAccessor, matchedSourceProperty.Name),
                    ExpressionType = matchedSourceProperty.Type
                };
            }

            //Non-direct (mapping like y.UserName = x.User.Name)
            var partialyMatchedSourceProperty =
                sourceProperties.Value.FirstOrDefault(x => targetName.StartsWith(x.Name, StringComparison.OrdinalIgnoreCase));
            if (partialyMatchedSourceProperty != null)
            {
                var subProperties = ObjectHelper.GetPublicPropertySymbols(partialyMatchedSourceProperty.Type).ToList();
                foreach (var subProperty in subProperties)
                {
                    if (targetName.Equals($"{partialyMatchedSourceProperty.Name}{subProperty.Name}",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        var firstLevelAccessor =
                            generator.MemberAccessExpression(sourceGlobalAccessor, partialyMatchedSourceProperty.Name);
                        return new MappingSource()
                        {
                            Expression =
                                (ExpressionSyntax) generator.MemberAccessExpression(firstLevelAccessor, subProperty.Name),
                            ExpressionType = subProperty.Type
                        };
                    }
                }
            }

            //Flattening with function eg. t.Total = s.GetTotal()
            var matchedSourceMethod =
                sourceMethods.Value.FirstOrDefault(x => x.Name.EndsWith(targetName, StringComparison.OrdinalIgnoreCase));
            if (matchedSourceMethod != null)
            {
                var sourceMethodAccessor = generator.MemberAccessExpression(sourceGlobalAccessor, matchedSourceMethod.Name);
                return new MappingSource()
                {
                    Expression = (ExpressionSyntax) generator.InvocationExpression(sourceMethodAccessor),
                    ExpressionType = matchedSourceMethod.ReturnType
                };
            }

            return null;
        }

        private MappingSource TryToUnwrapp(MappingSource mappingSource, ITypeSymbol targetType)
        {
            var sourceAccess = mappingSource.Expression as SyntaxNode;
            var conversion =  semanticModel.Compilation.ClassifyConversion(mappingSource.ExpressionType, targetType);
            if (conversion.Exists == false)
            {
                var wrapper = GetWrappingInfo(mappingSource.ExpressionType, targetType);
                if (wrapper.Type == WrapperInfoType.Property)
                {
                    return new MappingSource()
                    {
                        Expression = (ExpressionSyntax) generator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingProperty.Name),
                        ExpressionType = wrapper.UnwrappingProperty.Type
                    };
                }else if (wrapper.Type == WrapperInfoType.Method)
                {
                    var unwrappingMethodAccess = generator.MemberAccessExpression(sourceAccess, wrapper.UnwrappingMethod.Name);
                    ;
                    return new MappingSource()
                    {
                        Expression = (InvocationExpressionSyntax) generator.InvocationExpression(unwrappingMethodAccess),
                        ExpressionType = wrapper.UnwrappingMethod.ReturnType

                    };
                }

            }else if(conversion.IsExplicit)
            {
                return new MappingSource()
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