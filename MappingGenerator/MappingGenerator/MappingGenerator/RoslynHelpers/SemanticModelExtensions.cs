using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.RoslynHelpers
{
    public static class SemanticModelExtensions
    {
        private static readonly HashSet<SymbolKind> LocalSymbolKinds = new HashSet<SymbolKind>
        {
            SymbolKind.Local,
            SymbolKind.Parameter,
            SymbolKind.RangeVariable
        };

        public static IReadOnlyList<ISymbol> GetLocalSymbols(this SemanticModel semanticModel, SyntaxNode nodeFromScope,
            HashSet<SymbolKind> allowedSymbols = null)
        {
            var symbolsToSelect = allowedSymbols ?? LocalSymbolKinds;
            return semanticModel.LookupSymbols(nodeFromScope.GetLocation().SourceSpan.Start).Where(x => symbolsToSelect.Contains(x.Kind)).ToList();
        }


        public static ITypeSymbol GetTypeForSymbol(this SemanticModel semanticModel, ISymbol symbol)
        {
            if (symbol is IRangeVariableSymbol rangeVariableSymbol)
            {
                return GetRangeVariableType(semanticModel, rangeVariableSymbol);
            }

            var syntaxType = GetSyntaxType(symbol);
            if (syntaxType == null)
            {
                return null;
            }

            return semanticModel.GetTypeInfo(syntaxType).Type;
        }


        private static ITypeSymbol GetRangeVariableType(SemanticModel semanticModel, IRangeVariableSymbol symbol)
        {
            ITypeSymbol type = null;

            if (!symbol.Locations.IsEmpty)
            {
                var location = symbol.Locations.First();
                if (location.IsInSource && location.SourceTree == semanticModel.SyntaxTree)
                {
                    var token = location.SourceTree.GetRoot().FindToken(symbol.Locations.First().SourceSpan.Start);
                    var queryBody = GetQueryBody(token);
                    if (queryBody != null)
                    {
                        // To heuristically determine the type of the range variable in a query
                        // clause, we speculatively bind the name of the variable in the select
                        // or group clause of the query body.
                        var identifierName = SyntaxFactory.IdentifierName(symbol.Name);
                        type = semanticModel.GetSpeculativeTypeInfo(
                            queryBody.SelectOrGroup.Span.End - 1, identifierName, SpeculativeBindingOption.BindAsExpression).Type;
                    }

                    var identifier = token.Parent as IdentifierNameSyntax;
                    if (identifier != null)
                    {
                        type = semanticModel.GetTypeInfo(identifier).Type;
                    }
                }
            }

            return type;
        }

        private static QueryBodySyntax GetQueryBody(SyntaxToken token) =>
            token.Parent switch
            {
                FromClauseSyntax fromClause when fromClause.Identifier == token =>
                    fromClause.Parent as QueryBodySyntax ?? ((QueryExpressionSyntax)fromClause.Parent).Body,
                LetClauseSyntax letClause when letClause.Identifier == token =>
                    letClause.Parent as QueryBodySyntax,
                JoinClauseSyntax joinClause when joinClause.Identifier == token =>
                    joinClause.Parent as QueryBodySyntax,
                QueryContinuationSyntax continuation when continuation.Identifier == token =>
                    continuation.Body,
                _ => null
            };



        private static SyntaxNode GetSyntaxType(ISymbol candidate)
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

        public static bool MatchType(this SemanticModel semanticModel, ISymbol source, ITypeSymbol targetType)
        {
            var sourceSymbolType = semanticModel.GetTypeForSymbol(source);
            if (sourceSymbolType == null)
            {
                return false;
            }

            return sourceSymbolType.CanBeAssignedTo(targetType);
        }

        public static bool CanBeAssignedTo(this ITypeSymbol sourceSymbolType, ITypeSymbol targetType)
        {
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
    }
}