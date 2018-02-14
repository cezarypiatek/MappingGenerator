using Microsoft.CodeAnalysis;

namespace MappingGenerator
{
    internal class SymbolHelper
    {
        public static bool IsUpdateParameterFunction(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 2 && methodSymbol.ReturnsVoid;
        }

        public static bool IsUpdateThisObjectFunction(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 && methodSymbol.ReturnsVoid;
        }

        public static bool IsPureMappingFunction(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 && methodSymbol.ReturnsVoid == false;
        }

        public static bool IsMappingConstructor(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Length == 1 && methodSymbol.MethodKind == MethodKind.Constructor;
        }
    }
}