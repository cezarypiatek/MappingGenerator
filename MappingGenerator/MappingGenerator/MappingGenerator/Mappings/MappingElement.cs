using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.Mappings
{
    public abstract class MappingElement
    {
        public ExpressionSyntax Expression { get; set; }
        public AnnotatedType ExpressionType { get; set; }
    }

    public class TargetMappingElement : MappingElement
    {
        public bool OnlyIndirectInit { get; set; }
    }
    public class SourceMappingElement : MappingElement
    {
    }
}