using System.Collections.Generic;
using System.Linq;
using MappingGenerator.Mappings;
using MappingGenerator.Mappings.MappingImplementors;
using MappingGenerator.Mappings.SourceFinders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.MethodHelpers
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

        public IReadOnlyList<MappingElement> GetMatchedSources()
        {
            return Matches.Where(x => x.Source != null).Select(x => x.Source).ToList();
        }

        public void AddMatch(IParameterSymbol parameter, MappingElement  mappingSrc=null)
        {
            Matches.Add(new MatchedParameter()
            {
                Parameter = parameter,
                Source = mappingSrc
            });
            
            if (mappingSrc?.Expression != null)
            {
                MatchedCount++;
            }
        }

        public ArgumentListSyntax ToArgumentListSyntax(MappingEngine mappingEngine, MappingContext mappingContext, bool generateNamedParameters = true)
        {
            return Matches.Aggregate(SyntaxFactory.ArgumentList(), (list, match) =>
            {
                if (match.Source?.Expression == null && match.Parameter.IsOptional)
                {
                    generateNamedParameters = true;
                    return list;
                }

                var parameterType =new AnnotatedType(match.Parameter.Type);
                var expression = mappingEngine.MapExpression(match.Source, parameterType, mappingContext)?.Expression ?? mappingEngine.CreateDefaultExpression(parameterType.Type);
                var argument = generateNamedParameters
                    ? SyntaxFactory.Argument(SyntaxFactory.NameColon(match.Parameter.Name), SyntaxFactory.Token(SyntaxKind.None), expression)
                    : SyntaxFactory.Argument(expression);
                return list.AddArguments(argument);
            });
        }

        class MatchedParameter
        {
            public IParameterSymbol Parameter { get; set; }
            public MappingElement Source { get; set; }
        }
    }
}