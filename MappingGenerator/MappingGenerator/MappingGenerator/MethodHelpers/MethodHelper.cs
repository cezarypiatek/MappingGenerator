using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using MappingGenerator.Mappings;
using MappingGenerator.Mappings.SourceFinders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.MethodHelpers
{
    public static class MethodHelper
    {
        public static async Task<MatchedParameterList> FindBestParametersMatch(IMappingSourceFinder mappingSourceFinder, IEnumerable<ImmutableArray<IParameterSymbol>> overloadParameterSets, MappingContext mappingContext)
        {
            var matches = new List<MatchedParameterList>();
            foreach (var overloadParameterSet in overloadParameterSets)
            {
               var argumentMatch =  await FindArgumentsMatch(overloadParameterSet, mappingSourceFinder, mappingContext).ConfigureAwait(false);
               if (argumentMatch.HasAnyMatch())
               {
                   matches.Add(argumentMatch);
               }
            }

            return matches
                .OrderByDescending(x=> x.IsCompletlyMatched())
                .ThenByDescending(x => x.MatchedCount)
                .FirstOrDefault();
        }

        private static async Task<MatchedParameterList> FindArgumentsMatch(ImmutableArray<IParameterSymbol> parameters,
            IMappingSourceFinder mappingSourceFinder, MappingContext mappingContext)
        {
            var matchedArgumentList = new MatchedParameterList();
            foreach (var parameter in parameters)
            {
                var mappingSource = await mappingSourceFinder.FindMappingSource(parameter.Name, new AnnotatedType(parameter.Type), mappingContext).ConfigureAwait(false);
                matchedArgumentList.AddMatch(parameter, mappingSource);
            }
            return matchedArgumentList;
        }

        public static BaseMethodDeclarationSyntax WithOnlyBody(this BaseMethodDeclarationSyntax oldMethodDeclarationSyntax,
            BlockSyntax body)
        {
            switch (oldMethodDeclarationSyntax)
            {
                case ConstructorDeclarationSyntax constructorDeclarationSyntax:
                    return constructorDeclarationSyntax.WithBody(body).WithExpressionBody(null).WithSemicolonToken(default(SyntaxToken));
                case ConversionOperatorDeclarationSyntax conversionOperatorDeclarationSyntax:
                    return conversionOperatorDeclarationSyntax.WithBody(body).WithExpressionBody(null).WithSemicolonToken(default(SyntaxToken));
                case DestructorDeclarationSyntax destructorDeclarationSyntax:
                    return destructorDeclarationSyntax.WithBody(body).WithExpressionBody(null).WithSemicolonToken(default(SyntaxToken));
                case MethodDeclarationSyntax methodDeclarationSyntax:
                    return methodDeclarationSyntax.WithBody(body).WithExpressionBody(null).WithSemicolonToken(default(SyntaxToken));
                case OperatorDeclarationSyntax operatorDeclarationSyntax:
                    return operatorDeclarationSyntax.WithBody(body).WithExpressionBody(null).WithSemicolonToken(default(SyntaxToken));
                default:
                    return oldMethodDeclarationSyntax;
            }
        }
    }
}