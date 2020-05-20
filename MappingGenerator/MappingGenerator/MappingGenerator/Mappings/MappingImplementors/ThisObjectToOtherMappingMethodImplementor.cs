using System.Collections.Generic;
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

        public IEnumerable<SyntaxNode> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var targetType = methodSymbol.ReturnType;
            var newExpression = mappingEngine.MapExpression((ExpressionSyntax)generator.ThisExpression(), methodSymbol.ContainingType, targetType, new MappingContext());
            return new[] { generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation) };
        }
    }
}