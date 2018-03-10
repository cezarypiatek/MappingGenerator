using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator
{
    public static class MethodHelper
    {
        public static MatchedParameterList FindBestParametersMatch(IMethodSymbol methodSymbol, SemanticModel semanticModel,
            IMappingSourceFinder mappingSourceFinder)
        {
            var overloadParameterSets = methodSymbol.DeclaringSyntaxReferences.Select(ds =>
            {
                var overloadDeclaration = (MethodDeclarationSyntax) ds.GetSyntax();
                var overloadMethod = semanticModel.GetDeclaredSymbol(overloadDeclaration);
                return overloadMethod.Parameters;
            });
            return FindBestParametersMatch(mappingSourceFinder, overloadParameterSets);
        }

        public static MatchedParameterList FindBestParametersMatch(IMappingSourceFinder mappingSourceFinder, IEnumerable<ImmutableArray<IParameterSymbol>> overloadParameterSets)
        {
            return overloadParameterSets.Select(x=> FindArgumentsMatch(x, mappingSourceFinder))
                .Where(x=>x.HasAnyMatch())
                .OrderByDescending(x=> x.IsCompletlyMatched())
                .ThenByDescending(x => x.MatchedCount)
                .FirstOrDefault();
        }

        private static MatchedParameterList FindArgumentsMatch(ImmutableArray<IParameterSymbol> parameters, IMappingSourceFinder mappingSourceFinder)
        {
            var matchedArgumentList = new MatchedParameterList();
            foreach (var parameter in parameters)
            {
                var mappingSource = mappingSourceFinder.FindMappingSource(parameter.Name, parameter.Type);
                matchedArgumentList.AddMatch(parameter, mappingSource?.Expression);
            }
            return matchedArgumentList;
        }
    }
}