using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
namespace MappingGenerator.Mappings.SourceFinders
{
    public interface IMappingSourceFinder
    {
        Task<MappingElement> FindMappingSource(string targetName, AnnotatedType targetType, MappingContext mappingContext);
    }

    public class SyntaxFactoryExtensions
    {
        public static ExpressionSyntax CreateMemberAccessExpression(ExpressionSyntax expressionSyntax, bool isExpressionNullable, string memberName)
        {
            if (isExpressionNullable)
            {
                return ConditionalAccessExpression(expressionSyntax, MemberBindingExpression(IdentifierName(memberName))).WithAdditionalAnnotations(Simplifier.Annotation);
            }

            return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, expressionSyntax, IdentifierName(memberName));
        }
        public static ExpressionSyntax CreateMethodAccessExpression(ExpressionSyntax expressionSyntax, bool isExpressionNullable, string methodName, params ExpressionSyntax[] arguments)
        {
            if (isExpressionNullable)
            {
                var invocationExpression = InvocationExpression(MemberBindingExpression(IdentifierName(methodName)));
                if (arguments != null && arguments.Length >0)
                {
                    invocationExpression = invocationExpression.WithArgumentList(ArgumentList(SeparatedList(arguments.Select(Argument))));
                }
                return ConditionalAccessExpression(expressionSyntax, invocationExpression);
            }
            else
            {
                var invocationExpression = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, expressionSyntax, IdentifierName(methodName)));
                if (arguments != null && arguments.Length > 0)
                {
                    invocationExpression = invocationExpression.WithArgumentList(ArgumentList(SeparatedList(arguments.Select(Argument))));
                }
                return invocationExpression;
            }
        }

        public static ObjectCreationExpressionSyntax WithMembersInitialization(ObjectCreationExpressionSyntax objectCreationExpression, IReadOnlyList<AssignmentExpressionSyntax> assignments)
        {
            if (assignments.Count == 0)
            {
                return objectCreationExpression;
            }

            var fixedBracketsObjectCreation = objectCreationExpression.ArgumentList?.Arguments.Count > 0 ?  objectCreationExpression : objectCreationExpression.WithArgumentList(null);
            var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assignments))
                .FixInitializerExpressionFormatting(fixedBracketsObjectCreation);
            return fixedBracketsObjectCreation.WithInitializer(initializerExpressionSyntax);
        }
    }

    public class ObjectMembersMappingSourceFinder : IMappingSourceFinder
    {
        private readonly AnnotatedType sourceType;
        private readonly SyntaxNode sourceGlobalAccessor;
        private readonly Lazy<IReadOnlyList<IObjectField>> sourceProperties;
        private readonly Lazy<IReadOnlyList<IMethodSymbol>> sourceMethods;
        private readonly string potentialPrefix;
        private readonly Lazy<bool> isSourceTypeEnumerable;


        public ObjectMembersMappingSourceFinder(AnnotatedType sourceType, SyntaxNode sourceGlobalAccessor)
        {
            this.sourceType = sourceType;
            this.sourceGlobalAccessor = sourceGlobalAccessor;
            potentialPrefix = NameHelper.ToLocalVariableName(sourceGlobalAccessor.ToFullString());
            var sourceAllMembers = sourceType.Type.GetAllMembers();
            sourceProperties = new Lazy<IReadOnlyList<IObjectField>>(() =>ObjectFieldExtensions.GetObjectFields(sourceAllMembers).ToList());
            sourceMethods = new Lazy<IReadOnlyList<IMethodSymbol>>(()=> ObjectHelper.GetWithGetPrefixMethods(sourceAllMembers).ToList());
            isSourceTypeEnumerable = new Lazy<bool>(() => sourceType.Type.Interfaces.Any(x => x.ToDisplayString().StartsWith("System.Collections.Generic.IEnumerable<")));
        }

        public Task<MappingElement> FindMappingSource(string targetName, AnnotatedType targetType, MappingContext mappingContext)
        {
            var result = TryFindSource(targetName, mappingContext, sourceType) ?? TryFindSourceForEnumerable(targetName, targetType.Type);
            return Task.FromResult(result);
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
            var sourceMethodAccessor = SyntaxFactoryExtensions.CreateMethodAccessExpression((ExpressionSyntax)sourceGlobalAccessor, sourceType.CanBeNull, methodName);
            return new MappingElement()
            {
                Expression = sourceMethodAccessor,
                ExpressionType = new AnnotatedType(targetType)
            };
        }

        private MappingElement TryFindSource(string targetName, MappingContext mappingContext, AnnotatedType accessedVia)
        {
            //Direct 1-1 mapping
            var matchedSourceProperty = sourceProperties.Value
                .Where(x => IsMatched(targetName, x, potentialPrefix))
                .FirstOrDefault(p => p.CanBeGet(accessedVia.Type, mappingContext));

            if (matchedSourceProperty != null)
            {
                return new MappingElement
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
                var sourceMethodAccessor = SyntaxFactoryExtensions.CreateMethodAccessExpression((ExpressionSyntax)sourceGlobalAccessor, sourceType.CanBeNull, matchedSourceMethod.Name);
                return new MappingElement()
                {
                    Expression = sourceMethodAccessor,
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

        private static bool IsMatched(string targetName, IObjectField source, string sourceAccessPrefix)
        {
            var sanitizedName = SanitizeName(source.Name);
            var sanitizedTargetName = SanitizeName(targetName);
            var sanitizedNameWithPrefix = SanitizeName($"{sourceAccessPrefix}{source.Name}");
            return sanitizedName.Equals(sanitizedTargetName, StringComparison.OrdinalIgnoreCase) || sanitizedNameWithPrefix.Equals(sanitizedTargetName, StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeName(string name) => name.Replace("_", "");

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

            var subProperty = properties.Where(x => SanitizeName(targetName).StartsWith(SanitizeName($"{prefix}{x.Name}"), StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(p => p.CanBeGet(containingType, mappingContext));
            if (subProperty != null)
            {
                var currentNamePart = $"{prefix}{subProperty.Name}";
                var subPropertyAccessor = SyntaxFactoryExtensions.CreateMemberAccessExpression((ExpressionSyntax)currentAccessor, isCurrentAccessorNullable, subProperty.Name);
                var expressionCanBeNull = isCurrentAccessorNullable || subProperty.Type.CanBeNull;
                if (SanitizeName(targetName).Equals(SanitizeName(currentNamePart), StringComparison.OrdinalIgnoreCase))
                {
                    //Special Case: x.YValue = z.Y.Value
                    if (subProperty.Name == "Value" && SymbolHelper.IsNullable(containingType, out var _))
                    {
                        return new MappingElement
                        {
                            Expression = (ExpressionSyntax)currentAccessor,
                            ExpressionType = new AnnotatedType(containingType, true)
                        };
                    }
                   
                    return new MappingElement
                    {
                        Expression = subPropertyAccessor,
                        ExpressionType = new AnnotatedType(subProperty.Type.Type, expressionCanBeNull)
                    };
                }
                return FindSubPropertySource(targetName, subProperty.Type.Type, GetFields(subProperty.Type.Type),  subPropertyAccessor, mappingContext, expressionCanBeNull,  currentNamePart);
            }
            return null;
        }


        private readonly Dictionary<ITypeSymbol, IReadOnlyList<IObjectField>> _typeFieldsCache = new Dictionary<ITypeSymbol, IReadOnlyList<IObjectField>>();
        private IReadOnlyList<IObjectField> GetFields(ITypeSymbol typeSymbol)
        {
            if (_typeFieldsCache.TryGetValue(typeSymbol, out var fields))
            {
                return fields;
            }
            var freshFields = typeSymbol.GetObjectFields().ToList();
            _typeFieldsCache[typeSymbol] = freshFields;
            return freshFields;
        }
    }
}
