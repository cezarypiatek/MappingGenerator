using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public class MappingSourceFinder
    {
        private readonly SyntaxNode sourceGlobalAccessor;
        private readonly SyntaxGenerator generator;
        private readonly Lazy<IReadOnlyList<IPropertySymbol>> sourceProperties;
        private readonly Lazy<IReadOnlyList<IMethodSymbol>> sourceMethods;

        public MappingSourceFinder(ITypeSymbol sourceType, SyntaxNode sourceGlobalAccessor, SyntaxGenerator generator)
        {
            this.sourceGlobalAccessor = sourceGlobalAccessor;
            this.generator = generator;
            this.sourceProperties = new Lazy<IReadOnlyList<IPropertySymbol>>(() => ObjectHelper.GetPublicPropertySymbols(sourceType).ToList());
            this.sourceMethods = new Lazy<IReadOnlyList<IMethodSymbol>>(()=>  ObjectHelper.GetPublicGetMethods(sourceType).ToList());
        }

        public MappingSource FindMappingSource(string targetName)
        {
            //Direct 1-1 mapping
            var matchedSourceProperty = sourceProperties.Value.FirstOrDefault(x => x.Name == targetName);
            if (matchedSourceProperty != null)
            {
                return new MappingSource()
                {
                    Expression = (ExpressionSyntax)generator.MemberAccessExpression(sourceGlobalAccessor, matchedSourceProperty.Name),
                    ExpressionType = matchedSourceProperty.Type
                };
            }

            //Non-direct (mapping like y.UserName = x.User.Name)
            var partialyMatchedSourceProperty = sourceProperties.Value.FirstOrDefault(x => targetName.StartsWith(x.Name));
            if (partialyMatchedSourceProperty != null)
            {
                var subProperties = ObjectHelper.GetPublicPropertySymbols(partialyMatchedSourceProperty.Type).ToList();
                foreach (var subProperty in subProperties)
                {
                    if (targetName == $"{partialyMatchedSourceProperty.Name}{subProperty.Name}")
                    {
                        var firstLevelAccessor = generator.MemberAccessExpression(sourceGlobalAccessor, partialyMatchedSourceProperty.Name);
                        return new MappingSource()
                        {
                            Expression = (ExpressionSyntax)generator.MemberAccessExpression(firstLevelAccessor, subProperty.Name),
                            ExpressionType = subProperty.Type
                        };
                    }
                }
            }

            //Flattening with function eg. t.Total = s.GetTotal()
            var matchedSourceMethod = sourceMethods.Value.FirstOrDefault(x => x.Name.EndsWith(targetName));
            if (matchedSourceMethod != null)
            {
                var sourceMethodAccessor = generator.MemberAccessExpression(sourceGlobalAccessor, matchedSourceMethod.Name);
                return new MappingSource()
                {
                    Expression = (ExpressionSyntax)generator.InvocationExpression(sourceMethodAccessor),
                    ExpressionType = matchedSourceMethod.ReturnType
                };
            }

            return null;
        }
    }
}