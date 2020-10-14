using System.Collections.Generic;
using System.Threading.Tasks;
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

        public async Task<IReadOnlyList<SyntaxNode>> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel, MappingContext mappingContext)
        {
            var cloneMappingEngine = new CloneMappingEngine(semanticModel, generator);
            var sourceParameter = methodSymbol.Parameters[0];
            var sourceType = new AnnotatedType(sourceParameter.Type);
            var targetType = new AnnotatedType(methodSymbol.ReturnType);
            var newExpression = await cloneMappingEngine.MapExpression((ExpressionSyntax)generator.IdentifierName(sourceParameter.Name), sourceType, targetType, mappingContext).ConfigureAwait(false);
            return new[] { generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation) };
        }
    }
}