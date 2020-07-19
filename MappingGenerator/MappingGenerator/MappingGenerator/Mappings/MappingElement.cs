using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SmartAnalyzers.CSharpExtensions.Annotations;

namespace MappingGenerator.Mappings
{
    [InitOnly]
    public class MappingElement
    {
        public ExpressionSyntax Expression { get; set; }
        public AnnotatedType ExpressionType { get; set; }
    }
}