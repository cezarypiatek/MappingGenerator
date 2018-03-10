using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator
{
    public class LocalScopeMappingSourceFinder:IMappingSourceFinder
    {
        private readonly SemanticModel semanticModel;
        private readonly IReadOnlyList<ISymbol> localSymbols;

        public LocalScopeMappingSourceFinder(SemanticModel semanticModel, SyntaxNode nodeFromScope)
        {
            this.semanticModel = semanticModel;
            this.localSymbols = semanticModel.LookupSymbols(nodeFromScope.GetLocation().SourceSpan.Start).Where(x=>x.Kind == SymbolKind.Local || x.Kind == SymbolKind.Parameter).ToList();
        }

        public MappingSource FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            var candidate= localSymbols.FirstOrDefault(x => x.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
            if (candidate != null)
            {
                var type = GetType(candidate);
                if (type != null)
                {
                    return new MappingSource()
                    {
                        ExpressionType = type,
                        Expression = SyntaxFactory.IdentifierName(candidate.Name)
                    };
                }
            }
            return null;
        }

        private ITypeSymbol GetType(ISymbol symbol)
        {
            var syntaxType = GetSyntaxType(symbol);
            if (syntaxType == null)
            {
                return null;
            }
            return semanticModel.GetTypeInfo(syntaxType).Type;
        }

        private TypeSyntax GetSyntaxType(ISymbol candidate)
        {
            var candidateSyntax = candidate.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).FirstOrDefault();
            if (candidateSyntax is VariableDeclaratorSyntax variableDeclarator)
            {
                var variableDeclaration = variableDeclarator.FindContainer<VariableDeclarationSyntax>();
                return variableDeclaration.Type;
            }
            if (candidateSyntax is ParameterSyntax parameter)
            {
                return parameter.Type;
            }
            return null;
        }
    }
}