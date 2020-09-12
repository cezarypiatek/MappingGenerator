﻿using System.Collections.Generic;
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
            var symbolInfo = semanticModel.GetSymbolInfo(creationExpression.Type);
            if (symbolInfo.Symbol is INamedTypeSymbol instantiatedType)
            {
                return instantiatedType.Constructors.Select(x => x.Parameters);
            }
            return null;
        }

        public SyntaxNode WithArgumentList(ArgumentListSyntax argumentListSyntax)
        {
            return creationExpression.WithArgumentList(argumentListSyntax);
        }

        public SyntaxNode SourceNode => creationExpression;
        public ArgumentListSyntax Arguments => creationExpression.ArgumentList;
    }
}