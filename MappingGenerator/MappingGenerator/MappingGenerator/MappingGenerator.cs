using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public static class SyntaxGeneratorExtensions
    {
        public static SyntaxNode CompleteAssignmentStatement(this SyntaxGenerator generator, SyntaxNode left, SyntaxNode right)
        {
            var assignmentExpression = generator.AssignmentStatement(left, right);
            return generator.ExpressionStatement(assignmentExpression);
        }
        
        public static SyntaxNode ContextualReturnStatement(this SyntaxGenerator generator, SyntaxNode nodeToReturn, bool generatorContext)
        {
            if (generatorContext)
            {
                return SyntaxFactory.YieldStatement(SyntaxKind.YieldReturnStatement, nodeToReturn as ExpressionSyntax);
            }
            return generator.ReturnStatement(nodeToReturn);
        }
    }

    public class MappingGenerator
    {
        private readonly SyntaxGenerator generator;
        private static string[] SimpleTypes = new[] {"String", "Decimal"};
        private static char[] FobiddenSigns = new[] {'.', '[', ']', '(', ')'};

        public MappingGenerator(SyntaxGenerator generator)
        {
            this.generator = generator;
        }


        public IEnumerable<SyntaxNode> MapTypes(ITypeSymbol sourceType, ITypeSymbol targetType,
            SyntaxNode globalSourceAccessor, SyntaxNode globbalTargetAccessor = null, bool targetExists = false,
            bool generatorContext = false)
        {
            if (IsMappingBetweenCollections(targetType, sourceType))
            {
                var collectionMapping = MapCollections(globalSourceAccessor, sourceType, targetType);
                if (globbalTargetAccessor == null)
                {
                    yield return generator.ContextualReturnStatement(collectionMapping, generatorContext);    
                }
                else if(targetExists == false)
                {
                    yield return generator.CompleteAssignmentStatement(globbalTargetAccessor, collectionMapping);
                }
                yield break;
            }
            
            var targetLocalVariableName = globbalTargetAccessor ==null? ToLocalVariableName(targetType.Name): ToLocalVariableName(globbalTargetAccessor.ToFullString());
            if (targetExists == false)
            {
                var copyConstructor = FindCopyConstructor(targetType, sourceType);
                if (copyConstructor != null)
                {
                    var init = generator.ObjectCreationExpression(targetType, globalSourceAccessor);
                    yield return generator.ContextualReturnStatement(init, generatorContext);
                    yield break;
                }
                else
                {
                    var init = generator.ObjectCreationExpression(targetType);
                    yield return generator.LocalDeclarationStatement(targetLocalVariableName, init);     
                }
            }


            var matchedProperties = RetrieveMatchedProperties(sourceType, targetType).ToList();
            //Direct 1-1 mapping
            var localTargetIdentifier = targetExists? globbalTargetAccessor: generator.IdentifierName(targetLocalVariableName);
            foreach (var x in matchedProperties.Where(x => x.Source != null  && x.Target != null))
            {
                if (x.Target.SetMethod.DeclaredAccessibility != Accessibility.Public && globbalTargetAccessor.Kind() != SyntaxKind.ThisExpression)
                {
                    continue;
                }

                if (IsMappingBetweenCollections(x.Target.Type, x.Source.Type))
                {
                    var targetAccess = generator.MemberAccessExpression(localTargetIdentifier, x.Target.Name);
                    var sourceAccess = generator.MemberAccessExpression(globalSourceAccessor, x.Source.Name);
                    var collectionMapping = MapCollections(sourceAccess,  x.Source.Type,  x.Target.Type);
                    yield return generator.CompleteAssignmentStatement(targetAccess, collectionMapping);
                }
                else if (IsSimpleType(x.Target.Type) == false)
                {   
                    //TODO: What if both sides has the same type?
                    //TODO: Reverse flattening
                    var targetAccess = generator.MemberAccessExpression(localTargetIdentifier, x.Target.Name);
                    var sourceAccess = generator.MemberAccessExpression(globalSourceAccessor, x.Source.Name);
                    foreach (var complexPropertyMappingNode in MapTypes(x.Source.Type, x.Target.Type, sourceAccess, targetAccess))
                    {
                        yield return complexPropertyMappingNode;
                    }
                }
                else
                {
                    //TODO: What if boths sides has different type? 
                    // - Source is a wrapper of target (has only one property that match the target type)
                    // - There is another type of conversion between source and target
                    var targetAccess = generator.MemberAccessExpression(localTargetIdentifier, x.Target.Name);
                    var sourceAccess = generator.MemberAccessExpression(globalSourceAccessor, x.Source.Name);
                    yield return generator.CompleteAssignmentStatement(targetAccess, sourceAccess);
                }
            }

            //Non-direct (mapping like y.UserName = x.User.Name)
            var unmappedDirectly = matchedProperties.Where(x => x.Source != null  && x.Target == null);
            foreach (var unmapped in unmappedDirectly)
            {
                var targetsWithPartialNameAsSource = matchedProperties.Where(x => x.Target != null && x.Target.Name.StartsWith(unmapped.Source.Name)).Select(x => x.Target);
                var sourceProperties = GetPublicPropertySymbols(unmapped.Source.Type as INamedTypeSymbol).ToList();
                var sourceFirstLevelAccess = generator.MemberAccessExpression(globalSourceAccessor, unmapped.Source.Name);
                foreach (var target in targetsWithPartialNameAsSource)
                {   
                    foreach (var sourceProperty in sourceProperties)
                    {
                        if (target.Name == $"{unmapped.Source.Name}{sourceProperty.Name}")
                        {
                            var targetAccess = generator.MemberAccessExpression(localTargetIdentifier, target.Name);
                            var sourceAccess = generator.MemberAccessExpression(sourceFirstLevelAccess, sourceProperty.Name);
                            yield return generator.CompleteAssignmentStatement(targetAccess, sourceAccess);
                        }
                    }
                }
            }

            //Flattening with function eg. t.Total = s.GetTotal()
            var unmappedDirectlySourcesMethod = matchedProperties.Where(x => x.SourceGetMethod != null  && x.Target == null);
            foreach (var unmappedSourceMethod in unmappedDirectlySourcesMethod)
            {
                var target = matchedProperties.FirstOrDefault(x => x.Target!=null && unmappedSourceMethod.SourceGetMethod.Name.EndsWith(x.Target.Name))?.Target;
                if (target != null)
                {
                    if (target.SetMethod.DeclaredAccessibility != Accessibility.Public && globbalTargetAccessor.Kind() != SyntaxKind.ThisExpression)
                    {
                        continue;
                    }

                    var targetAccess = generator.MemberAccessExpression(localTargetIdentifier, target.Name);
                    var sourceAccess = generator.MemberAccessExpression(globalSourceAccessor, unmappedSourceMethod.SourceGetMethod.Name);
                    var sourceCall = generator.InvocationExpression(sourceAccess);
                    yield return generator.CompleteAssignmentStatement(targetAccess, sourceCall);
                }
            }

            if (globbalTargetAccessor == null)
            {
                yield return generator.ContextualReturnStatement(localTargetIdentifier, generatorContext);    
            }
            else if(targetExists == false)
            {
                yield return generator.CompleteAssignmentStatement(globbalTargetAccessor, localTargetIdentifier);
            }
        }

        private SyntaxNode MapCollections(SyntaxNode sourceAccess, ITypeSymbol sourceListType, ITypeSymbol targetListType)
        {
            var isReadolyCollection = targetListType.Name == "ReadOnlyCollection";
            var sourceListElementType = GetElementType(sourceListType);
            var targetListElementType = GetElementType(targetListType);
            if (IsSimpleType(sourceListElementType) || sourceListElementType == targetListElementType)
            {
                var toListInvocation = AddMaterializeCollectionInvocation(generator, sourceAccess, targetListType);
                return WrapInReadonlyCollectionIfNecessary(isReadolyCollection, toListInvocation, generator);
            }
            var selectAccess = generator.MemberAccessExpression(sourceAccess, "Select");
            var lambdaParameterName = ToSingularLocalVariableName(ToLocalVariableName(sourceListElementType.Name));
            var listElementMappingStms = MapTypes(sourceListElementType, targetListElementType, generator.IdentifierName(lambdaParameterName));
            var selectInvocation = generator.InvocationExpression(selectAccess, generator.ValueReturningLambdaExpression(lambdaParameterName, listElementMappingStms));
            var toList = AddMaterializeCollectionInvocation(generator, selectInvocation, targetListType);
            return WrapInReadonlyCollectionIfNecessary(isReadolyCollection, toList, generator);
        }

        private static ITypeSymbol GetElementType(ITypeSymbol collectionType)
        {
            switch (collectionType)
            {
                case INamedTypeSymbol namedType:
                    return namedType.TypeArguments[0];
                case IArrayTypeSymbol arrayType:
                    return arrayType.ElementType;
                default:
                    throw new NotSupportedException("Unknown collection type");
            }
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

        private static bool IsMappingBetweenCollections(ITypeSymbol targetClassSymbol, ITypeSymbol sourceClassSymbol)
        {
            return (HasInterface(targetClassSymbol, "System.Collections.Generic.ICollection<T>") || targetClassSymbol.Kind == SymbolKind.ArrayType)
                   && (HasInterface(sourceClassSymbol, "System.Collections.Generic.IEnumerable<T>") || sourceClassSymbol.Kind == SymbolKind.ArrayType);
        }

        private static IMethodSymbol FindCopyConstructor(ITypeSymbol type, ITypeSymbol constructorParameterType)
        {
            if (type is INamedTypeSymbol namedType)
            {
                return namedType.Constructors.FirstOrDefault(c => c.Parameters.Length == 1 && c.Parameters[0].Type == constructorParameterType);
            }
            return null;
        }

        private static bool IsSimpleType(ITypeSymbol type)
        {
            return type.IsValueType || SimpleTypes.Contains(type.Name);
        }

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

        private static bool HasInterface(ITypeSymbol xt, string interfaceName)
        {
            return xt.OriginalDefinition.AllInterfaces.Any(x => x.ToDisplayString() == interfaceName);
        }

        private static bool IsPublicGetMethod(ISymbol x)
        {
            return x is IMethodSymbol mSymbol
                   && mSymbol.ReturnsVoid == false
                   && mSymbol.Parameters.Length == 0
                   && x.DeclaredAccessibility == Accessibility.Public
                   && x.IsImplicitlyDeclared == false
                   && mSymbol.MethodKind == MethodKind.Ordinary
                ;
        }

        private static bool IsPublicPropertySymbol(ISymbol x)
        {
            if (x.Kind != SymbolKind.Property)
            {
                return false;
            }

            if (x is IPropertySymbol mSymbol)
            {
                if (mSymbol.IsStatic || mSymbol.IsIndexer || mSymbol.DeclaredAccessibility != Accessibility.Public)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static IEnumerable<IPropertySymbol> GetPublicPropertySymbols(ITypeSymbol source)
        {
            return Enumerable.SelectMany<ITypeSymbol, ISymbol>(GetBaseTypesAndThis(source), x=> x.GetMembers()).Where(IsPublicPropertySymbol).OfType<IPropertySymbol>();
        }

        private static IEnumerable<IMethodSymbol> GetPublicGetMethods(ITypeSymbol source)
        {
            return Enumerable.SelectMany<ITypeSymbol, ISymbol>(GetBaseTypesAndThis(source), x=> x.GetMembers()).Where(IsPublicGetMethod).OfType<IMethodSymbol>();
        }

        private  static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(ITypeSymbol type)
        {
            var current = type;
            while (current != null && IsSystemObject(current) == false)
            {
                yield return current;
                current = current.BaseType;
            }
        }

        private static bool IsSystemObject(ITypeSymbol current)
        {
            return current.Name == "Object" && current.ContainingNamespace.Name =="System";
        }

        private static IEnumerable<MatchedPropertySymbols> RetrieveMatchedProperties(ITypeSymbol source, ITypeSymbol target)
        {
            var propertiesMap = new SortedDictionary<string, MatchedPropertySymbols>();

            foreach (var mSymbol in GetPublicPropertySymbols(source))
            {
                if (!propertiesMap.ContainsKey(mSymbol.Name))
                {
                    // If class definition is invalid, it may happen that we get multiple properties with the same name
                    // Ignore all but first
                    propertiesMap.Add(mSymbol.Name, new MatchedPropertySymbols() { Source = mSymbol });
                }
            }

            foreach (var methodSymbol in GetPublicGetMethods(source))
            {
                if (propertiesMap.ContainsKey(methodSymbol.Name) == false)
                {
                    propertiesMap.Add(methodSymbol.Name, new MatchedPropertySymbols()
                    {
                        SourceGetMethod = methodSymbol
                    });
                }
            }

            foreach (var mSymbol in GetPublicPropertySymbols(target))
            {
                if (mSymbol.IsReadOnly)
                {
                    continue;
                }

                MatchedPropertySymbols sourceProperty = null;
                if (!propertiesMap.TryGetValue(mSymbol.Name, out sourceProperty))
                {
                    propertiesMap.Add(mSymbol.Name, new MatchedPropertySymbols { Target = mSymbol });
                }
                else if (sourceProperty.Target == null)
                {
                    // If class definition is invalid, it may happen that we get multiple properties with the same name
                    // Ignore all but first
                    sourceProperty.Target = mSymbol;
                }
            }

            return propertiesMap.Values;
        }
    }
}