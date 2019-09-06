using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using NUnit.Framework;
using RoslynTestKit;

namespace MappingGenerator.Test.MethodParameterSuggestion
{
    public class MethodParametersSuggestionProviderTest: CompletionProviderFixture
    {
        [Test]
        public void should_be_able_to_get_completion()
        {
            TestCompletion(MethodParameterSuggestionTestCases._001_SuggestOuterMethodParameters, new []
            {
                "firstName, lastName, age",
                "firstName:firstName, lastName:lastName, age:age"
            });
        }


        protected override string LanguageName => LanguageNames.CSharp;
        protected override CompletionProvider CreateProvider()
        {
            return  new MethodParametersSuggestionProvider();
        }
    }
}