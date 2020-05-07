using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace MappingGenerator.Mappings
{
    public class MappingForeachElement
    {
        public List<SyntaxNode> SyntaxNodes { get; } = new List<SyntaxNode>();
        public string TargetName { get; set; }
    }
}