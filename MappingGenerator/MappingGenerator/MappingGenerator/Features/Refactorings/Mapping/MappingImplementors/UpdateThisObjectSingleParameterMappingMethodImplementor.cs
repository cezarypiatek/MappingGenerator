﻿using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Features.Refactorings.Mapping.MappingImplementors
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

        public IEnumerable<SyntaxNode> GenerateImplementation(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var source = methodSymbol.Parameters[0];
            var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator);
            var targets = ObjectHelper.GetFieldsThaCanBeSetPrivately(methodSymbol.ContainingType);
            return mappingEngine.MapUsingSimpleAssignment(targets, sourceFinder);
        }
    }
}