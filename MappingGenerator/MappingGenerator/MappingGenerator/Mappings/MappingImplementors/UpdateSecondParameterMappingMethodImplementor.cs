using System.Collections.Generic;
using System.Threading.Tasks;
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

        public async Task<IReadOnlyList<SyntaxNode>> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator,
            SemanticModel semanticModel, MappingContext mappingContext)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator);
            var source = methodSymbol.Parameters[0];
            var target = methodSymbol.Parameters[1];
            var mappingTargetHelper = new MappingTargetHelper();
            var targets = mappingTargetHelper.GetFieldsThaCanBeSetPublicly(target.Type, mappingContext);
            var sourceFinder = new ObjectMembersMappingSourceFinder(new AnnotatedType(source.Type), generator.IdentifierName(source.Name));
            return await mappingEngine.MapUsingSimpleAssignment(targets, new SingleSourceMatcher(sourceFinder), mappingContext, globalTargetAccessor: generator.IdentifierName(target.Name)).ConfigureAwait(false);
        }
    }
}