using System.Collections.Generic;
using MappingGenerator.Mappings.SourceFinders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator.Mappings.MappingImplementors
{
    internal class IdentityMappingMethodImplementor: IMappingMethodImplementor
    {
        public bool CanImplement(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 && methodSymbol.ReturnType.Equals(methodSymbol.Parameters[0].Type);
        }

        public IEnumerable<SyntaxNode> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel, MappingContext mappingContext)
        {
            var cloneMappingEngine = new CloneMappingEngine(semanticModel, generator);
            var sourceParameter = methodSymbol.Parameters[0];
            var sourceType = new AnnotatedType(sourceParameter.Type);
            var targetType = new AnnotatedType(methodSymbol.ReturnType);
            var newExpression = cloneMappingEngine.MapExpression((ExpressionSyntax)generator.IdentifierName(sourceParameter.Name), sourceType, targetType, mappingContext);
            return new[] { generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation) };
        }
    }
}