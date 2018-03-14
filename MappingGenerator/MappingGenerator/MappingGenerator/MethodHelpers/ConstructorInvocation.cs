using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.MethodHelpers
{
    public class ConstructorInvocation:IInvocation
    {
        private readonly ObjectCreationExpressionSyntax creationExpression;
        
        public ConstructorInvocation(ObjectCreationExpressionSyntax creationExpression)
        {
            this.creationExpression = creationExpression;
        }

        public IEnumerable<ImmutableArray<IParameterSymbol>> GetOverloadParameterSets(SemanticModel semanticModel)
        {
            var instantiatedType = (INamedTypeSymbol)semanticModel.GetSymbolInfo(creationExpression.Type).Symbol;
            return instantiatedType.Constructors.Select(x => x.Parameters);
        }

        public SyntaxNode WithArgumentList(ArgumentListSyntax argumentListSyntax)
        {
            return creationExpression.WithArgumentList(argumentListSyntax);
        }

        public SyntaxNode SourceNode => creationExpression;
        public ArgumentListSyntax Arguments => creationExpression.ArgumentList;
    }
}