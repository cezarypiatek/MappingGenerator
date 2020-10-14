using System.Collections.Generic;
using System.Threading.Tasks;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator.Mappings.MappingImplementors
{
    class ThisObjectToOtherMappingMethodImplementor:IMappingMethodImplementor
    {
        public bool CanImplement(IMethodSymbol methodSymbol)
        {
            return methodSymbol.IsStatic == false &&
                   methodSymbol.Parameters.Length == 0 &&
                   methodSymbol.ReturnsVoid == false &&
                   ObjectHelper.IsSimpleType(methodSymbol.ReturnType) == false;
        }

        public async Task<IReadOnlyList<SyntaxNode>> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator,
            SemanticModel semanticModel, MappingContext mappingContext)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator);
            var destinationType = new AnnotatedType(methodSymbol.ReturnType);
            var sourceType = new AnnotatedType(methodSymbol.ContainingType);
            var newExpression = await mappingEngine.MapExpression((ExpressionSyntax)generator.ThisExpression(), sourceType, destinationType, mappingContext).ConfigureAwait(false);
            return new[] { generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation) };
        }
    }
}