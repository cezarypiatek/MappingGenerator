using MappingGenerator.Features.Suggestions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using NUnit.Framework;
using RoslynTestKit;

namespace MappingGenerator.Test.MethodParameterSuggestion
{
    public class MethodParametersSuggestionProviderTest: CompletionProviderFixture
    {
        [Test]
        public void should_be_able_to_get_completion_with_outer_method_parameters()
        {
            TestCompletion(MethodParameterSuggestionTestCases._001_SuggestOuterMethodParameters, new []
            {
                "firstName, lastName, age",
                "firstName:firstName, lastName:lastName, age:age"
            });
        }

        [Test]
        public void should_be_able_to_get_completion_with_outer_method_parameters_and_local()
        {
            TestCompletion(MethodParameterSuggestionTestCases._002_SuggestOuterMethodParametersAndLocal, new []
            {
                "firstName, lastName, age, parent",
                "firstName:firstName, lastName:lastName, age:age, parent:parent"
            });
        }

        [Test]
        public void should_be_able_to_get_completion_with_outer_method_parameters_and_outer_type_members()
        {
            TestCompletion(MethodParameterSuggestionTestCases._003_SuggestOuterMethodParametersAndMembers, new []
            {
                "firstName, lastName, age, parent",
                "firstName:firstName, lastName:lastName, age:age, parent:parent"
            });
        }

        [Test]
        public void should_be_able_to_get_completion_with_variables_that_match_only_type_when_single_candidate()
        {
            TestCompletion(MethodParameterSuggestionTestCases._004_FallbackByTypeIfSingleCandidate, new []
            {
                "firstName, lastName, age, firstParent",
                "firstName:firstName, lastName:lastName, age:age, parent:firstParent"
            });
        }

        [Test]
        public void should_be_able_to_get_completion_with_variables_that_match_only_type_when_single_candidate_by_interface()
        {
            TestCompletion(MethodParameterSuggestionTestCases._005_FallbackByTypeIfSingleCandidateInterface, new []
            {
                "firstName, lastName, age, firstParent",
                "firstName:firstName, lastName:lastName, age:age, parent:firstParent"
            });
        }

        [Test]
        public void should_be_able_to_get_completion_with_variables_that_match_only_type_when_single_candidate_by_base_class()
        {
            TestCompletion(MethodParameterSuggestionTestCases._005_FallbackByTypeIfSingleCandidateInterface, new []
            {
                "firstName, lastName, age, firstParent",
                "firstName:firstName, lastName:lastName, age:age, parent:firstParent"
            });
        }

        [Test]
        public void should_be_able_to_get_completion_with_variables_that_match_only_type_when_single_candidate_by_interface_inheritance()
        {
            TestCompletion(MethodParameterSuggestionTestCases._007_FallbackByTypeIfSingleCandidateInterfaceInheritance, new[]
            {
                "firstName, lastName, age, firstParent",
                "firstName:firstName, lastName:lastName, age:age, parent:firstParent"
            });
        }

        [Test]
        public void should_be_able_to_get_completion_with_variables_that_match_only_type_when_single_candidate_by_base_class_inheritance()
        {
            TestCompletion(MethodParameterSuggestionTestCases._008_FallbackByTypeIfSingleCandidateBaseClassInheritance, new[]
            {
                "firstName, lastName, age, firstParent",
                "firstName:firstName, lastName:lastName, age:age, parent:firstParent"
            });
        }
        
        
        [Test]
        public void should_be_able_to_get_completion_with_variables_for_extension_method()
        {
            TestCompletion(MethodParameterSuggestionTestCases._009_SuggestParamsForExtensionMethod, new[]
            {
                "firstName, lastName, age",
                "firstName:firstName, lastName:lastName, age:age"
            });
        }
        
        
        [Test]
        public void should_be_able_to_get_completion_that_match_implementation_to_interface()
        {
            TestCompletion(MethodParameterSuggestionTestCases._010_SuggestParamsWithInterface, new[]
            {
                "firstName, lastName, provider",
                "firstName:firstName, lastName:lastName, other:provider"
            });
        }


        protected override string LanguageName => LanguageNames.CSharp;
        protected override CompletionProvider CreateProvider()
        {
            return  new MethodParametersSuggestionProvider();
        }
    }
}