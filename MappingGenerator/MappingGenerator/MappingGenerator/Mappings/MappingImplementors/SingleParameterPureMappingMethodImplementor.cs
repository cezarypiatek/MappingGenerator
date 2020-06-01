using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator.Mappings.MappingImplementors
{
    internal class SingleParameterPureMappingMethodImplementor: IMappingMethodImplementor
    {
        public bool CanImplement(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 && methodSymbol.ReturnsVoid == false;
        }

        public IEnumerable<SyntaxNode> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator,
            SemanticModel semanticModel, MappingContext mappingContext)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator);
            var source = methodSymbol.Parameters[0];
            var targetType = methodSymbol.ReturnType;
            var newExpression = mappingEngine.MapExpression((ExpressionSyntax)generator.IdentifierName(source.Name),
                source.Type, targetType, mappingContext);
            return new[] { generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation) };
        }
    }
}