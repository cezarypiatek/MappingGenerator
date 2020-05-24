using System.Collections.Generic;
using System.Linq;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.Mappings
{
    public static class CustomConversionHelper
    {
        public static IEnumerable<IMethodSymbol> FindCustomConversionMethods(IMethodSymbol methodSymbol)
        {
            var userDefinedConversions = methodSymbol.ContainingType.GetBaseTypesAndThis()
                .SelectMany(t => t.GetMembers().OfType<IMethodSymbol>().Where(IsConversionMethod));
            return userDefinedConversions;
        }

        private static bool IsConversionMethod(IMethodSymbol methodSymbol)
        {
            return methodSymbol.ReturnsVoid == false && 
                   methodSymbol.Parameters.Length == 1 && 
                   methodSymbol.Parameters[0] is {} parameter && 
                   parameter.IsOptional == false && 
                   parameter.RefKind == RefKind.None ;
        }
    }
}