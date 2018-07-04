using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
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
        private readonly Lazy<IReadOnlyList<IPropertySymbol>> sourceProperties;
        private readonly Lazy<IReadOnlyList<IMethodSymbol>> sourceMethods;

        public ObjectMembersMappingSourceFinder(ITypeSymbol sourceType, SyntaxNode sourceGlobalAccessor, SyntaxGenerator generator)
        {
            this.sourceType = sourceType;
            this.sourceGlobalAccessor = sourceGlobalAccessor;
            this.generator = generator;
            this.sourceProperties = new Lazy<IReadOnlyList<IPropertySymbol>>(() => ObjectHelper.GetPublicPropertySymbols(sourceType)
                .Where(property => property.GetMethod!=null)
                .ToList());
            this.sourceMethods = new Lazy<IReadOnlyList<IMethodSymbol>>(()=>  ObjectHelper.GetPublicGetMethods(sourceType).ToList());
        }

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            var mappingSource = FindSource(targetName);
            return mappingSource;
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
                    return new MappingElement()
                    {
                        Expression = subPropertyAccessor,
                        ExpressionType = subProperty.Type
                    };
                }
                return FindSubPropertySource(targetName, subProperty.Type, ObjectHelper.GetPublicPropertySymbols(subProperty.Type),  subPropertyAccessor, currentNamePart);
            }
            return null;
        }

       

      
    }
}