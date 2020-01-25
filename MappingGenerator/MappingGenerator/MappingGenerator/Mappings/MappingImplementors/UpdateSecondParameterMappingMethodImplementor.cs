using System.Collections.Generic;
using MappingGenerator.Mappings.MappingMatchers;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingImplementors
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
            return mappingEngine.MapUsingSimpleAssignment(targets, new SingleSourceMatcher(sourceFinder), globalTargetAccessor: generator.IdentifierName(target.Name));
        }
    }
}