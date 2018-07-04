using System.Linq;
using MappingGenerator.MethodHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public class MappingElement
    {
        public ExpressionSyntax Expression { get; set; }
        public ITypeSymbol ExpressionType { get; set; }
    }
}