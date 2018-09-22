using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Pluralize.NET;

namespace MappingGenerator
{
    public static class NameHelper
    {
        private static readonly char[] ForbiddenSigns = new[] {'.', '[', ']', '(', ')'};
        private static readonly Pluralizer Pluralizer = new Pluralizer();

        public static string CreateLambdaParameterName(SyntaxNode sourceList)
        {
            var originalName = sourceList.ToFullString();
            var localVariableName = ToLocalVariableName(originalName);
            var finalName = ToSingularLocalVariableName(localVariableName);
            if (originalName == finalName)
            {
                return $"{finalName}Element";
            }
            return finalName;
        }

        public static string ToLocalVariableName(string proposalLocalName)
        {
            var withoutForbiddenSigns = string.Join("",proposalLocalName.Trim().Split(ForbiddenSigns).Where(x=> string.IsNullOrWhiteSpace(x) == false).Select(x=>
            {
                var cleanElement = x.Trim();
                return $"{cleanElement.Substring(0, 1).ToUpper()}{cleanElement.Substring(1)}";
            }));
            return $"{withoutForbiddenSigns.Substring(0, 1).ToLower()}{withoutForbiddenSigns.Substring(1)}";
        }

        private static readonly string[] CollectionSynonyms = new[] {"List", "Collection", "Set", "Queue", "Dictionary", "Stack", "Array"};

        private static string ToSingularLocalVariableName(string proposalLocalName)
        {
            if (CollectionSynonyms.Any(x=> x.Equals(proposalLocalName, StringComparison.OrdinalIgnoreCase)))
            {
                return "item";
            }

            foreach (var collectionName in CollectionSynonyms)
            {
                if (proposalLocalName.EndsWith(collectionName, StringComparison.OrdinalIgnoreCase))
                {
                    proposalLocalName = proposalLocalName.Substring(0, proposalLocalName.Length - collectionName.Length);
                    break;
                }
            }

            if (IsPluralForm(proposalLocalName))
            {
                return Pluralizer.Singularize(proposalLocalName);
            }

            return proposalLocalName;
        }

        private static bool IsPluralForm(string proposalLocalName)
        {
            return string.Equals(Pluralizer.Pluralize(proposalLocalName), proposalLocalName, StringComparison.OrdinalIgnoreCase);
        }
    }
}