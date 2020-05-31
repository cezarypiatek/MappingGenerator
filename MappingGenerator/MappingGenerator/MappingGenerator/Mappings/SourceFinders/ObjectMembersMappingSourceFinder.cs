using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.SourceFinders
{
    public interface IMappingSourceFinder
    {
        MappingElement FindMappingSource(string targetName, ITypeSymbol targetType, MappingContext mappingContext);
    }

    public class ObjectMembersMappingSourceFinder : IMappingSourceFinder
    {
        private readonly ITypeSymbol sourceType;
        private readonly SyntaxNode sourceGlobalAccessor;
        private readonly SyntaxGenerator generator;
        private readonly Lazy<IReadOnlyList<IObjectField>> sourceProperties;
        private readonly Lazy<IReadOnlyList<IMethodSymbol>> sourceMethods;
        private readonly string potentialPrefix;
        private readonly Lazy<bool> isSourceTypeEnumerable;


        public ObjectMembersMappingSourceFinder(ITypeSymbol sourceType, SyntaxNode sourceGlobalAccessor, SyntaxGenerator generator)
        {
            this.sourceType = sourceType;
            this.sourceGlobalAccessor = sourceGlobalAccessor;
            this.generator = generator;
            this.potentialPrefix = NameHelper.ToLocalVariableName(sourceGlobalAccessor.ToFullString());
            this.sourceProperties = new Lazy<IReadOnlyList<IObjectField>>(() => sourceType.GetObjectFields().ToList());
            this.sourceMethods = new Lazy<IReadOnlyList<IMethodSymbol>>(()=> ObjectHelper.GetWithGetPrefixMethods(sourceType).ToList());
            this.isSourceTypeEnumerable = new Lazy<bool>(() => sourceType.Interfaces.Any(x => x.ToDisplayString().StartsWith("System.Collections.Generic.IEnumerable<")));
        }

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType, MappingContext mappingContext)
        {
            return TryFindSource(targetName, mappingContext, sourceType) ?? TryFindSourceForEnumerable(targetName, targetType);
        }


        //TODO: Acquire semantic model and try to search through extensions methods with no arguments
        private MappingElement TryFindSourceForEnumerable(string targetName, ITypeSymbol targetType)
        {
            if (isSourceTypeEnumerable.Value)
            {
                if (targetName == "Any" && targetType.Name == "Boolean")
                {
                    return CreateMappingElementFromExtensionMethod(targetType, "Any");
                }

                if (targetName == "Count" && targetType.Name == "Int32")
                {
                    return CreateMappingElementFromExtensionMethod(targetType, "Count");
                }
            }

            return null;
        }

        private MappingElement CreateMappingElementFromExtensionMethod(ITypeSymbol targetType, string methodName)
        {
            var sourceMethodAccessor = generator.MemberAccessExpression(sourceGlobalAccessor, methodName);
            return new MappingElement()
            {
                Expression = (ExpressionSyntax) generator.InvocationExpression(sourceMethodAccessor),
                ExpressionType = targetType
            };
        }

        private MappingElement TryFindSource(string targetName, MappingContext mappingContext, ITypeSymbol accessedVia)
        {
            //Direct 1-1 mapping
            var matchedSourceProperty = sourceProperties.Value
                .Where(x => x.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) || $"{potentialPrefix}{x.Name}".Equals(targetName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(p => p.CanBeGet(accessedVia, mappingContext));

            if (matchedSourceProperty != null)
            {
                return new MappingElement()
                {
                    Expression = (ExpressionSyntax) generator.MemberAccessExpression(sourceGlobalAccessor, matchedSourceProperty.Name),
                    ExpressionType = matchedSourceProperty.Type
                };
            }

            //Non-direct (mapping like y.UserName = x.User.Name)
            var source = FindSubPropertySource(targetName, sourceType, sourceProperties.Value, sourceGlobalAccessor, mappingContext);
            if (source != null)
            {
                return source;
            }

            //Flattening with function eg. t.Total = s.GetTotal()
            var matchedSourceMethod = sourceMethods.Value.Where((x => x.Name.EndsWith(targetName, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(m => mappingContext.AccessibilityHelper.IsSymbolAccessible(m, accessedVia));
            if (matchedSourceMethod != null)
            {
                var sourceMethodAccessor = generator.MemberAccessExpression(sourceGlobalAccessor, matchedSourceMethod.Name);
                return new MappingElement()
                {
                    Expression = (ExpressionSyntax) generator.InvocationExpression(sourceMethodAccessor),
                    ExpressionType = matchedSourceMethod.ReturnType
                };
            }

            // HIGHLY SPECULATIVE: Expanding acronyms: UserName = u.Name
            if (string.IsNullOrWhiteSpace(potentialPrefix) == false && potentialPrefix == potentialPrefix.ToLowerInvariant() && targetName != potentialPrefix)
            {
                var acronym = GetAcronym(targetName).ToLowerInvariant();
                if (acronym.StartsWith(potentialPrefix))
                {
                    var rest =  acronymPattern.Split(targetName).Skip(potentialPrefix.Length);
                    var newTarget = $"{potentialPrefix}{string.Join("", rest)}";
                    return TryFindSource(newTarget, mappingContext, accessedVia);
                }
            }
            return null;
        }

        private readonly Regex acronymPattern = new Regex(@"(?<!^)(?=[A-Z])", RegexOptions.Compiled);

        private string GetAcronym(string targetName)
        {
            var capitalLetters = targetName.Where(char.IsUpper).ToArray();
            return new string(capitalLetters);
        }

        private MappingElement FindSubPropertySource(string targetName, ITypeSymbol containingType,
            IEnumerable<IObjectField> properties, SyntaxNode currentAccessor, MappingContext mappingContext,
            string prefix = null)
        {
            if (ObjectHelper.IsSimpleType(containingType))
            {
                return null;
            }

            var subProperty = properties.Where(x => targetName.StartsWith($"{prefix}{x.Name}", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(p => p.CanBeGet(containingType, mappingContext));
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
                return FindSubPropertySource(targetName, subProperty.Type, subProperty.Type.GetObjectFields(),  subPropertyAccessor, mappingContext, currentNamePart);
            }
            return null;
        }
    }
}
