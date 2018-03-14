using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.MethodHelpers
{
    public interface IInvocation
    {
        IEnumerable<ImmutableArray<IParameterSymbol>> GetOverloadParameterSets(SemanticModel semanticModel);
        SyntaxNode WithArgumentList(ArgumentListSyntax argumentListSyntax);
        SyntaxNode SourceNode { get; }
        ArgumentListSyntax Arguments { get; }
    }
}