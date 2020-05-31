using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Generic;

namespace MappingGenerator.Mappings.MappingImplementors
{
    internal class SingleParameterForeachMappingMethodImplementor : IMappingMethodImplementor
    {
        public bool CanImplement(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 &&
                methodSymbol.ReturnsVoid == false &&
                ContainsCollectionWithoutSetter(methodSymbol.ReturnType);
        }

        public IEnumerable<SyntaxNode> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel, MappingContext mappingContext)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var source = methodSymbol.Parameters[0];
            var targetType = methodSymbol.ReturnType;

            var syntaxNodes = new List<SyntaxNode>();
            syntaxNodes.AddRange(mappingEngine.MapUsingForeachExpression((ExpressionSyntax)generator.IdentifierName(source.Name), null, source.Type, targetType, mappingContext).SyntaxNodes);
            syntaxNodes.Add(generator.ReturnStatement(generator.IdentifierName(NameHelper.ToLocalVariableName(targetType.Name))).WithAdditionalAnnotations(Formatter.Annotation));

            return syntaxNodes;
        }

        private bool ContainsCollectionWithoutSetter(ITypeSymbol type)
        {
            foreach (var member in ObjectHelper.GetPublicPropertySymbols(type))
            {
                if (ObjectHelper.IsSimpleType(member.Type))
                {
                    continue;
                }

                var isCollection = MappingHelper.IsCollection(member.Type);
                if (isCollection && member.SetMethod == null)
                {
                    return true;
                }

                if (ContainsCollectionWithoutSetter(isCollection ? MappingHelper.GetElementType(member.Type) : member.Type))
                {
                    return true;
                }
            }
            return false;
        }
    }
}