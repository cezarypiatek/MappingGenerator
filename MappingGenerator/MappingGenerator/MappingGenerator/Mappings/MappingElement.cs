using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.Mappings
{
    public class MappingElement
    {
        public ExpressionSyntax Expression { get; set; }
        public ITypeSymbol ExpressionType { get; set; }
    }
}