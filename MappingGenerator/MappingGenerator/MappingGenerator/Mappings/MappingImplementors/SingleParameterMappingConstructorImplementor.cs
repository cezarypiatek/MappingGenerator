using System.Collections.Generic;
using MappingGenerator.Mappings.MappingMatchers;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingImplementors
{
    class SingleParameterMappingConstructorImplementor:IMappingMethodImplementor
    {
        public bool CanImplement(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 && SymbolHelper.IsConstructor(methodSymbol);
        }

        public IEnumerable<SyntaxNode> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator,
            SemanticModel semanticModel, MappingContext mappingContext)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator);
            var sourceParameter = methodSymbol.Parameters[0];
            var sourceFinder = new ObjectMembersMappingSourceFinder(new AnnotatedType(sourceParameter.Type), generator.IdentifierName(sourceParameter.Name), generator);
            var targets = MappingTargetHelper.GetFieldsThaCanBeSetFromConstructor(methodSymbol.ContainingType, mappingContext);
            return mappingEngine.MapUsingSimpleAssignment(targets, new SingleSourceMatcher(sourceFinder), mappingContext);
        }
    }
}
