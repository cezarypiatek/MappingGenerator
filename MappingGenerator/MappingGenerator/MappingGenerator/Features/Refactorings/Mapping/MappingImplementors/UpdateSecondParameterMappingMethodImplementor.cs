using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Features.Refactorings.Mapping.MappingImplementors
{
    class UpdateSecondParameterMappingMethodImplementor:IMappingMethodImplementor
    {
        public bool CanImplement(IMethodSymbol methodSymbol)
        {
            if (SymbolHelper.IsConstructor(methodSymbol))
            {
                return false;
            }
            return methodSymbol.Parameters.Length == 2 && methodSymbol.ReturnsVoid;
        }

        public IEnumerable<SyntaxNode> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var source = methodSymbol.Parameters[0];
            var target = methodSymbol.Parameters[1];
            var targets = ObjectHelper.GetFieldsThaCanBeSetPublicly(target.Type, methodSymbol.ContainingAssembly);
            var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator);
            return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder, globalTargetAccessor: generator.IdentifierName(target.Name));
        }
    }
}