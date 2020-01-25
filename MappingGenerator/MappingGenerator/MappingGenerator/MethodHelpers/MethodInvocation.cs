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
            var methodSymbol = GetMethodOverloads(semanticModel, out var methodOverloads);
            if (methodOverloads == null)
            {
                return null;
            }


            if (methodSymbol.IsExtensionMethod  && invocationExpression.Expression is MemberAccessExpressionSyntax)
            {
                return methodOverloads.Where( x=> x.IsExtensionMethod).Select(x => x.Parameters.Skip(1).ToImmutableArray()).ToList();
            }
            
            return methodOverloads.Select(x => x.Parameters).ToList();
        }

        private IMethodSymbol GetMethodOverloads(SemanticModel semanticModel, out IEnumerable<IMethodSymbol> methodOverloads)
        {
            var methodSymbol = semanticModel.GetSymbolInfo(invocationExpression).CandidateSymbols.OfType<IMethodSymbol>()
                .FirstOrDefault();
            methodOverloads = methodSymbol?.ContainingType.GetMembers(methodSymbol.Name).OfType<IMethodSymbol>();
            return methodSymbol;
        }

        public SyntaxNode WithArgumentList(ArgumentListSyntax argumentListSyntax)
        {
            return invocationExpression.WithArgumentList(argumentListSyntax);
        }

        public SyntaxNode SourceNode => invocationExpression;
        public ArgumentListSyntax Arguments => invocationExpression.ArgumentList;
    }
}
