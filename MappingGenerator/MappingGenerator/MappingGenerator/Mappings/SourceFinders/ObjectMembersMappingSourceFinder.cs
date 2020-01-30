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
        MappingElement FindMappingSource(string targetName, ITypeSymbol targetType);
    }

    public class ObjectMembersMappingSourceFinder : IMappingSourceFinder
    {
        private readonly ITypeSymbol sourceType;
        private readonly SyntaxNode sourceGlobalAccessor;
        private readonly SyntaxGenerator generator;
        private readonly Lazy<IReadOnlyList<IPropertySymbol>> sourceProperties;
        private readonly Lazy<IReadOnlyList<IMethodSymbol>> sourceMethods;
        private readonly string potentialPrefix;
        private readonly Lazy<bool> isSourceTypeEnumerable;


        public ObjectMembersMappingSourceFinder(ITypeSymbol sourceType, SyntaxNode sourceGlobalAccessor, SyntaxGenerator generator)
        {
            this.sourceType = sourceType;
            this.sourceGlobalAccessor = sourceGlobalAccessor;
            this.generator = generator;
            this.potentialPrefix = NameHelper.ToLocalVariableName(sourceGlobalAccessor.ToFullString());
            this.sourceProperties = new Lazy<IReadOnlyList<IPropertySymbol>>(() => GetPublicPropertySymbols(sourceType)
                .Where(property => property.GetMethod!=null)
                .ToList());
            this.sourceMethods = new Lazy<IReadOnlyList<IMethodSymbol>>(()=> ObjectHelper.GetPublicGetMethods(sourceType).ToList());
            this.isSourceTypeEnumerable = new Lazy<bool>(() => sourceType.Interfaces.Any(x => x.ToDisplayString().StartsWith("System.Collections.Generic.IEnumerable<")));
        }

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            return TryFindSource(targetName) ?? TryFindSourceForEnumerable(targetName, targetType);
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

        private MappingElement TryFindSource(string targetName)
        {
            //Direct 1-1 mapping
            var matchedSourceProperty = sourceProperties.Value.FirstOrDefault(x => x.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) || $"{potentialPrefix}{x.Name}".Equals(targetName, StringComparison.OrdinalIgnoreCase));
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

            // HIGHLY SPECULATIVE: Expanding acronyms: UserName = u.Name
            if (string.IsNullOrWhiteSpace(potentialPrefix) == false && potentialPrefix == potentialPrefix.ToLowerInvariant() && targetName != potentialPrefix)
            {
                var acronym = GetAcronym(targetName).ToLowerInvariant();
                if (acronym.StartsWith(potentialPrefix))
                {
                    var rest = Regex.Split(targetName, @"(?<!^)(?=[A-Z])").Skip(potentialPrefix.Length);
                    var newTarget = $"{potentialPrefix}{string.Join("", rest)}";
                    return TryFindSource(newTarget);
                }
            }
            return null;
        }

        private string GetAcronym(string targetName)
        {
            var capitalLetters = targetName.Where(char.IsUpper).ToArray();
            return new string(capitalLetters);
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
                return FindSubPropertySource(targetName, subProperty.Type, GetPublicPropertySymbols(subProperty.Type),  subPropertyAccessor, currentNamePart);
            }
            return null;
        }

        private static IEnumerable<IPropertySymbol> GetPublicPropertySymbols(ITypeSymbol source)
        {
            return source.GetBaseTypesAndThis().SelectMany(x => x.GetMembers()).OfType<IPropertySymbol>().Where(IsPublicPropertySymbol);
        }

        private static bool IsPublicPropertySymbol(IPropertySymbol x)
        {
            if (x.IsStatic || x.IsIndexer || x.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
            return true;
        }
    }
}