using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public class MatchedParameterList
    {
        bool HasUnmatched { get; set; }

        public bool IsCompletlyMatched()
        {
            return Matches.Count > 0 && HasUnmatched == false;
        }

        public bool HasAnyMatch()
        {
            return MatchedCount > 0;
        }

        public int MatchedCount { get; private set; }

        private List<MatchedParameter> Matches { get; } = new List<MatchedParameter>();

        public void AddMatch(IParameterSymbol parameter, ExpressionSyntax matchedExpression=null)
        {
            Matches.Add(new MatchedParameter()
            {
                Parameter = parameter,
                MatchedExpression = matchedExpression
            });
            
            if (matchedExpression != null)
            {
                MatchedCount++;
            }
        }

        public ArgumentListSyntax ToArgumentListSyntax(SyntaxGenerator generator, bool generateNamedParameters=true)
        {
            return Matches.Aggregate(SyntaxFactory.ArgumentList(), (list, match) =>
            {
                if (generateNamedParameters && match.MatchedExpression == null && match.Parameter.IsOptional)
                {
                    return list;
                }

                var expression = match.MatchedExpression ?? (ExpressionSyntax)generator.DefaultExpression((ITypeSymbol) match.Parameter.Type);
                var argument = generateNamedParameters
                    ? SyntaxFactory.Argument(SyntaxFactory.NameColon((string) match.Parameter.Name), SyntaxFactory.Token(SyntaxKind.None), expression)
                    : SyntaxFactory.Argument(expression);
                return list.AddArguments(argument);
            });
        }

        class MatchedParameter
        {
            public IParameterSymbol Parameter { get; set; }
            public ExpressionSyntax MatchedExpression { get; set; }
        }
    }
}