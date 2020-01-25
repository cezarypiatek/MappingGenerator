using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Mappings.MappingImplementors
{
    public interface IMappingMethodImplementor
    {
        bool CanImplement(IMethodSymbol methodSymbol);

        IEnumerable<SyntaxNode> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel);
    }
}