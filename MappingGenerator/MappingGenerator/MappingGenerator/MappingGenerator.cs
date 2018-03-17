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
    public class MappingGenerator
    {
        private readonly SyntaxGenerator generator;
        private readonly SemanticModel semanticModel;
        private static char[] FobiddenSigns = new[] {'.', '[', ']', '(', ')'};

        public MappingGenerator(SyntaxGenerator generator, SemanticModel semanticModel)
        {
            this.generator = generator;
            this.semanticModel = semanticModel;
        }

        public IEnumerable<SyntaxNode> MapTypes(ITypeSymbol sourceType, ITypeSymbol targetType,
            SyntaxNode globalSourceAccessor, SyntaxNode globbalTargetAccessor = null, bool targetExists = false,
            bool generatorContext = false,
            bool isConstructorContext = false)
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
            var mappingSourceFinder = new ObjectMembersMappingSourceFinder(sourceType, globalSourceAccessor, generator, semanticModel);
            if (targetExists == false)
            {
                var mappingConstructorParameters = FindMappingConstructorParameters(targetType, sourceType, mappingSourceFinder, (ExpressionSyntax) globalSourceAccessor);
                if (mappingConstructorParameters != null)
                {
                    var init = generator.ObjectCreationExpression(targetType, mappingConstructorParameters.Arguments);
                    if (globbalTargetAccessor == null)
                    {
                        yield return generator.ContextualReturnStatement(init, generatorContext);
                    }
                    else
                    {
                        yield return generator.CompleteAssignmentStatement(globbalTargetAccessor, init);
                    }
                    yield break;
                }
                else
                {
                    var init = generator.ObjectCreationExpression(targetType);
                    yield return generator.LocalDeclarationStatement(targetLocalVariableName, init);     
                }
            }

            var localTargetIdentifier = targetExists? globbalTargetAccessor: generator.IdentifierName(targetLocalVariableName);
            foreach (var targetProperty in ObjectHelper.GetPublicPropertySymbols(targetType))
            {
                if (CanBeSet(targetProperty, globbalTargetAccessor, isConstructorContext) == false)
                {
                    continue;
                }

                var mappingSource = mappingSourceFinder.FindMappingSource(targetProperty.Name, targetProperty.Type);
                if (mappingSource == null)
                {
                    continue;
                }

                var mappingTarget = new MappingElement()
                {
                    Expression = (ExpressionSyntax)generator.MemberAccessExpression(localTargetIdentifier, targetProperty.Name),
                    ExpressionType = targetProperty.Type
                };

                foreach (var syntaxNode in Map(mappingSource, mappingTarget))
                {
                    yield return syntaxNode;
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

        private static bool CanBeSet(IPropertySymbol targetProperty, SyntaxNode globbalTargetAccessor, bool isConstructorContext)
        {
            var isPrivateField = targetProperty.SetMethod?.DeclaredAccessibility != Accessibility.Public;
            var isOutsideThisScope = globbalTargetAccessor?.Kind() != SyntaxKind.ThisExpression;
            var isReadonlyProperty = targetProperty.IsReadonlyProperty();

            if (isPrivateField && isOutsideThisScope)
            {
                return false;
            }

            if (isReadonlyProperty && isConstructorContext == false)
            {
                return false;
            }

            return true;
        }

        public IEnumerable<SyntaxNode> Map(MappingElement source, MappingElement target)
        {
            if (IsMappingBetweenCollections(target.ExpressionType, source.ExpressionType))
            {
                var collectionMapping = MapCollections(source.Expression, source.ExpressionType, target.ExpressionType);
                yield return generator.CompleteAssignmentStatement(target.Expression, collectionMapping);
            }
            else if (ObjectHelper.IsSimpleType(target.ExpressionType) == false)
            {
                //TODO: What if both sides has the same type?
                //TODO: Reverse flattening
                foreach (var complexPropertyMappingNode in MapTypes(source.ExpressionType, target.ExpressionType,  source.Expression, target.Expression))
                {
                    yield return complexPropertyMappingNode;
                }
            }
            else
            {
                yield return generator.CompleteAssignmentStatement(target.Expression, source.Expression);
            }
        }

        private SyntaxNode MapCollections(SyntaxNode sourceAccess, ITypeSymbol sourceListType, ITypeSymbol targetListType)
        {
            var isReadolyCollection = targetListType.Name == "ReadOnlyCollection";
            var sourceListElementType = GetElementType(sourceListType);
            var targetListElementType = GetElementType(targetListType);
            if (ObjectHelper.IsSimpleType(sourceListElementType) || sourceListElementType == targetListElementType)
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

        private ArgumentListSyntax FindMappingConstructorParameters(ITypeSymbol targetType, ITypeSymbol sourceType, ObjectMembersMappingSourceFinder mappingSourceFinder, ExpressionSyntax globalSourceAccessor)
        {
            if (targetType is INamedTypeSymbol namedType)
            {
                var directlyMappingConstructor = namedType.Constructors.FirstOrDefault(c => c.Parameters.Length == 1 && c.Parameters[0].Type == sourceType);
                if (directlyMappingConstructor != null)
                {
                    return SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(globalSourceAccessor));
                }
                var constructorOverloadParameterSets = namedType.Constructors.Select(x=>x.Parameters);
                return MethodHelper.FindBestParametersMatch(mappingSourceFinder, constructorOverloadParameterSets)?.ToArgumentListSyntax(generator);
            }
            return null;
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
    }
}