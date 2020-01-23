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
        public bool AllowMatchOnlyByTypeWhenSingleCandidate { get; set; }

        public static LocalScopeMappingSourceFinder FromScope(SemanticModel semanticModel, SyntaxNode nodeFromScope, HashSet<SymbolKind> allowedSymbols = null)
        {
            var symbols = semanticModel.GetLocalSymbols(nodeFromScope, allowedSymbols);
            return new LocalScopeMappingSourceFinder(semanticModel, symbols);
        }

        public LocalScopeMappingSourceFinder(SemanticModel semanticModel, IMethodSymbol methodSymbol)
        {
            this.semanticModel = semanticModel;
            this.localSymbols = methodSymbol.Parameters;
        }

        public LocalScopeMappingSourceFinder(SemanticModel semanticModel, IReadOnlyList<ISymbol> localSymbols)
        {
            this.semanticModel = semanticModel;
            this.localSymbols = localSymbols;
        }

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            var candidate= localSymbols.FirstOrDefault(x => x.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
            if (candidate != null)
            {
                var type = semanticModel.GetTypeForSymbol(candidate);
                if (type != null)
                {
                    return new MappingElement()
                    {
                        ExpressionType = type,
                        Expression = CreateIdentifierName(candidate)
                    };
                }
            }

            if (AllowMatchOnlyByTypeWhenSingleCandidate)
            {
                var byTypeCandidates = localSymbols.Where(x => MatchType(x, targetType)).ToList();
                if (byTypeCandidates.Count == 1)
                {
                    var byTypeCandidate = byTypeCandidates[0];
                    var type = semanticModel.GetTypeForSymbol(byTypeCandidate);
                    if (type != null)
                    {
                        return new MappingElement()
                        {
                            ExpressionType = type,
                            Expression = CreateIdentifierName(byTypeCandidate)
                        };
                    }
                }
            }
            return null;
        }

        private bool MatchType(ISymbol source, ITypeSymbol targetType)
        {
            var sourceSymbolType = semanticModel.GetTypeForSymbol(source);
            if (sourceSymbolType == null)
            {
                return false;
            }

            if (sourceSymbolType.GetBaseTypesAndThis().Any(t => t.Equals(targetType)))
            {
                return true;
            }

            if (targetType.TypeKind == TypeKind.Interface)
            {
                if (sourceSymbolType.OriginalDefinition.AllInterfaces.Any(i => i.Equals(targetType)))
                {
                    return true;
                }
            }

            return false;
        }


        private static IdentifierNameSyntax CreateIdentifierName(ISymbol candidate)
        {
            var identifier = candidate.Name;
            bool isAnyKeyword = SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
                                || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None;

            if (isAnyKeyword)
            {
                return SyntaxFactory.IdentifierName("@"+identifier);
            }

            return SyntaxFactory.IdentifierName(identifier);
        }

    }
}