using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.SourceFinders
{
    public interface IMappingSourceFinder
    {
        MappingElement FindMappingSource(string targetName, AnnotatedType targetType, MappingContext mappingContext);
    }

    public class SyntaxFactoryExtensions
    {
        public static MemberAccessExpressionSyntax CreateMemberAccessExpression(ExpressionSyntax expressionSyntax, bool isExpressionNullable, string memberName)
        {
            var type = isExpressionNullable ? SyntaxKind.ConditionalAccessExpression: SyntaxKind.SimpleMemberAccessExpression;
            return SyntaxFactory.MemberAccessExpression(type, expressionSyntax, SyntaxFactory.IdentifierName(memberName));
        }
    }

    public class ObjectMembersMappingSourceFinder : IMappingSourceFinder
    {
        private readonly AnnotatedType sourceType;
        private readonly SyntaxNode sourceGlobalAccessor;
        private readonly SyntaxGenerator generator;
        private readonly Lazy<IReadOnlyList<IObjectField>> sourceProperties;
        private readonly Lazy<IReadOnlyList<IMethodSymbol>> sourceMethods;
        private readonly string potentialPrefix;
        private readonly Lazy<bool> isSourceTypeEnumerable;


        public ObjectMembersMappingSourceFinder(AnnotatedType sourceType, SyntaxNode sourceGlobalAccessor, SyntaxGenerator generator)
        {
            this.sourceType = sourceType;
            this.sourceGlobalAccessor = sourceGlobalAccessor;
            this.generator = generator;
            this.potentialPrefix = NameHelper.ToLocalVariableName(sourceGlobalAccessor.ToFullString());
            var sourceAllMembers = sourceType.Type.GetAllMembers();
            this.sourceProperties = new Lazy<IReadOnlyList<IObjectField>>(() =>ObjectFieldExtensions.GetObjectFields(sourceAllMembers).ToList());
            this.sourceMethods = new Lazy<IReadOnlyList<IMethodSymbol>>(()=> ObjectHelper.GetWithGetPrefixMethods(sourceAllMembers).ToList());
            this.isSourceTypeEnumerable = new Lazy<bool>(() => sourceType.Type.Interfaces.Any(x => x.ToDisplayString().StartsWith("System.Collections.Generic.IEnumerable<")));
        }

        public MappingElement FindMappingSource(string targetName, AnnotatedType targetType, MappingContext mappingContext)
        {
            return TryFindSource(targetName, mappingContext, sourceType) ?? TryFindSourceForEnumerable(targetName, targetType.Type);
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
            var sourceMethodAccessor = SyntaxFactoryExtensions.CreateMemberAccessExpression((ExpressionSyntax)sourceGlobalAccessor, sourceType.CanBeNull, methodName);
            return new MappingElement()
            {
                Expression = (ExpressionSyntax) generator.InvocationExpression(sourceMethodAccessor),
                ExpressionType = new AnnotatedType(targetType)
            };
        }

        private MappingElement TryFindSource(string targetName, MappingContext mappingContext, AnnotatedType accessedVia)
        {
            //Direct 1-1 mapping
            var matchedSourceProperty = sourceProperties.Value
                .Where(x => x.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) || $"{potentialPrefix}{x.Name}".Equals(targetName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(p => p.CanBeGet(accessedVia.Type, mappingContext));

            if (matchedSourceProperty != null)
            {
                return new MappingElement()
                {
                    Expression = SyntaxFactoryExtensions.CreateMemberAccessExpression((ExpressionSyntax)sourceGlobalAccessor, accessedVia.CanBeNull, matchedSourceProperty.Name),
                    ExpressionType = new AnnotatedType(matchedSourceProperty.Type.Type, accessedVia.CanBeNull || matchedSourceProperty.Type.CanBeNull)
                };
            }

            //Non-direct (mapping like y.UserName = x.User.Name)
            var source = FindSubPropertySource(targetName, sourceType.Type, sourceProperties.Value, sourceGlobalAccessor, mappingContext, accessedVia.CanBeNull);
            if (source != null)
            {
                return source;
            }

            //Flattening with function eg. t.Total = s.GetTotal()
            var matchedSourceMethod = sourceMethods.Value.Where((x => x.Name.EndsWith(targetName, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(m => mappingContext.AccessibilityHelper.IsSymbolAccessible(m, accessedVia.Type));
            if (matchedSourceMethod != null)
            {
                var sourceMethodAccessor = SyntaxFactoryExtensions.CreateMemberAccessExpression((ExpressionSyntax)sourceGlobalAccessor, sourceType.CanBeNull, matchedSourceMethod.Name);
                return new MappingElement()
                {
                    Expression = (ExpressionSyntax) generator.InvocationExpression(sourceMethodAccessor),
                    ExpressionType = new AnnotatedType(matchedSourceMethod.ReturnType, sourceType.CanBeNull || matchedSourceMethod.CanBeNull())
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

        private MappingElement FindSubPropertySource(string targetName, ITypeSymbol containingType, IEnumerable<IObjectField> properties, SyntaxNode currentAccessor, MappingContext mappingContext, bool isCurrentAccessorNullable,  string prefix = null)
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
                var subPropertyAccessor = SyntaxFactoryExtensions.CreateMemberAccessExpression((ExpressionSyntax)currentAccessor, isCurrentAccessorNullable, subProperty.Name);
                var expressionCanBeNull = isCurrentAccessorNullable || subProperty.Type.CanBeNull;
                if (targetName.Equals(currentNamePart, StringComparison.OrdinalIgnoreCase))
                {
                    
                    return new MappingElement
                    {
                        Expression = subPropertyAccessor,
                        ExpressionType = new AnnotatedType(subProperty.Type.Type, expressionCanBeNull)
                    };
                }
                return FindSubPropertySource(targetName, subProperty.Type.Type, subProperty.Type.Type.GetObjectFields(),  subPropertyAccessor, mappingContext, expressionCanBeNull,  currentNamePart);
            }
            return null;
        }
    }


    public static class NullableExtensions
    {
        public static bool CanBeNull(this IMethodSymbol methodSymbol) => methodSymbol.ReturnType.CanBeNull();
        public static bool CanBeNull(this IPropertySymbol propertySymbol) => propertySymbol.Type.CanBeNull();
        public static bool CanBeNull(this IFieldSymbol fieldSymbol) => fieldSymbol.CanBeNull();
        public static bool CanBeNull(this ITypeSymbol typeSymbol) => typeSymbol.NullableAnnotation == NullableAnnotation.Annotated;
        public static AnnotatedType GetAnnotatedType(this TypeInfo typeInfo) => new AnnotatedType(typeInfo.Type);
        public static AnnotatedType GetAnnotatedTypeForConverted(this TypeInfo typeInfo) => new AnnotatedType(typeInfo.ConvertedType);
    }


}
