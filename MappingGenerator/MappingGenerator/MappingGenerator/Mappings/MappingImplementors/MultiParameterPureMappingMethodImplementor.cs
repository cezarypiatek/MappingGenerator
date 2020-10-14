using System.Collections.Generic;
using System.Threading.Tasks;
using MappingGenerator.Mappings.MappingMatchers;
using MappingGenerator.Mappings.SourceFinders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator.Mappings.MappingImplementors
{
    class MultiParameterPureMappingMethodImplementor: IMappingMethodImplementor
    {
        public bool CanImplement(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length > 1 && methodSymbol.ReturnsVoid == false;
        }

        public async Task<IReadOnlyList<SyntaxNode>> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator,
            SemanticModel semanticModel, MappingContext mappingContext)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator);
            var targetType = methodSymbol.ReturnType;
            var sourceFinder = new LocalScopeMappingSourceFinder(semanticModel, methodSymbol);
            var objectCreationExpressionSyntax = (ObjectCreationExpressionSyntax)generator.ObjectCreationExpression(targetType.StripNullability());
            var newExpression = await mappingEngine.AddInitializerWithMappingAsync(objectCreationExpressionSyntax, new SingleSourceMatcher(sourceFinder), targetType, mappingContext).ConfigureAwait(false);
            return new[] { generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation) };
        }
    }
}