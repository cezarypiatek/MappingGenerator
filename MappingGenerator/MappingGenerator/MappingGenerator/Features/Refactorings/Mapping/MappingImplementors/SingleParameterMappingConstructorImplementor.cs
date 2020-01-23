using System.Collections.Generic;
using MappingGenerator.MappingMatchers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Features.Refactorings.Mapping.MappingImplementors
{
    class SingleParameterMappingConstructorImplementor:IMappingMethodImplementor
    {
        public bool CanImplement(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 && SymbolHelper.IsConstructor(methodSymbol);
        }

        public IEnumerable<SyntaxNode> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var source = methodSymbol.Parameters[0];
            var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator);
            var targets = ObjectHelper.GetFieldsThaCanBeSetFromConstructor(methodSymbol.ContainingType);
            return mappingEngine.MapUsingSimpleAssignment(targets, new SingleSourceMatcher(sourceFinder));
        }
    }
}