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
        public static ArgumentListSyntax FindBestArgumentsMatch(IMethodSymbol methodSymbol, SemanticModel semanticModel, IMappingSourceFinder mappingSourceFinder)
        {
            var overloadParameterSets = methodSymbol.DeclaringSyntaxReferences.Select(ds =>
            {
                var overloadDeclaration = (MethodDeclarationSyntax) ds.GetSyntax();
                var overloadMethod = semanticModel.GetDeclaredSymbol(overloadDeclaration);
                return overloadMethod.Parameters;
            });
            return FindBestArgumentsMatch(mappingSourceFinder, overloadParameterSets);
        }

        public static ArgumentListSyntax FindBestArgumentsMatch(IMappingSourceFinder mappingSourceFinder, IEnumerable<ImmutableArray<IParameterSymbol>> overloadParameterSets)
        {
            return overloadParameterSets
                .Select(x=> FindArgumentsMatch(x, mappingSourceFinder))
                .Where(x => x.Arguments.Count > 0)
                .OrderByDescending(argumentList => argumentList.Arguments.Count)
                .FirstOrDefault();
        }

        private static ArgumentListSyntax FindArgumentsMatch(ImmutableArray<IParameterSymbol> parameters, IMappingSourceFinder mappingSourceFinder)
        {
            var argumentList = SyntaxFactory.ArgumentList();
            foreach (var parameter in parameters)
            {
                var mappingSource = mappingSourceFinder.FindMappingSource(parameter.Name, parameter.Type);
                if (mappingSource != null)
                {
                    var argument = SyntaxFactory.Argument(SyntaxFactory.NameColon(parameter.Name), SyntaxFactory.Token(SyntaxKind.None), mappingSource.Expression);
                    argumentList = argumentList.AddArguments(argument);
                }
            }
            return argumentList;
        }
    }
}