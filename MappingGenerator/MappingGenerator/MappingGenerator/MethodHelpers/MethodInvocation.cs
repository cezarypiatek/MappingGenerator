using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.MethodHelpers
{
    public class MethodInvocation:IInvocation
    {
        private readonly InvocationExpressionSyntax invocationExpression;

        public MethodInvocation(InvocationExpressionSyntax invocationExpression)
        {
            this.invocationExpression = invocationExpression;
        }

        public IEnumerable<ImmutableArray<IParameterSymbol>> GetOverloadParameterSets(SemanticModel semanticModel)
        {
            var methodSymbol = semanticModel.GetSymbolInfo(invocationExpression).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
            if (methodSymbol == null)
            {
                return null;
            }
            return MethodHelper.GetOverloadParameterSets(methodSymbol, semanticModel);
        }

        public SyntaxNode WithArgumentList(ArgumentListSyntax argumentListSyntax)
        {
            return invocationExpression.WithArgumentList(argumentListSyntax);
        }

        public SyntaxNode SourceNode => invocationExpression;
        public ArgumentListSyntax Arguments => invocationExpression.ArgumentList;
    }
}
