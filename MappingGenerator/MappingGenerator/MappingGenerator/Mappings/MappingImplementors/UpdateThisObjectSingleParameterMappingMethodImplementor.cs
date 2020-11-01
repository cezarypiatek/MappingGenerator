using System.Collections.Generic;
using System.Threading.Tasks;
using MappingGenerator.Mappings.MappingMatchers;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingImplementors
{
    class UpdateThisObjectSingleParameterMappingMethodImplementor:IMappingMethodImplementor
    {
        public bool CanImplement(IMethodSymbol methodSymbol)
        {
            if (SymbolHelper.IsConstructor(methodSymbol))
            {
                return false;
            }
            return methodSymbol.Parameters.Length == 1 && methodSymbol.ReturnsVoid;
        }

        public async Task<IReadOnlyList<SyntaxNode>> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator,
            SemanticModel semanticModel, MappingContext mappingContext)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator);
            var source = methodSymbol.Parameters[0];
            var sourceFinder = new ObjectMembersMappingSourceFinder(new AnnotatedType(source.Type), generator.IdentifierName(source.Name), generator);
            var mappingTargetHelper = new MappingTargetHelper();
            var targets = mappingTargetHelper.GetFieldsThaCanBeSetPrivately(methodSymbol.ContainingType, mappingContext);
            return await mappingEngine.MapUsingSimpleAssignment(targets, new SingleSourceMatcher(sourceFinder), mappingContext).ConfigureAwait(false);
        }
    }
}
