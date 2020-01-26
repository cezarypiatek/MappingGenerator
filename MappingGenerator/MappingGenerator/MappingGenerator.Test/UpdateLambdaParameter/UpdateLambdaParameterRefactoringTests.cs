using MappingGenerator.Features.Refactorings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using NUnit.Framework;
using RoslynTestKit;
using static MappingGenerator.Test.UpdateLambdaParameter.UpdateLambdaParameterRefactoringTestCases;

namespace MappingGenerator.Test.UpdateLambdaParameter
{
    public class UpdateLambdaParameterRefactoringTests: CodeRefactoringTestFixture
    {
        protected override string LanguageName => LanguageNames.CSharp;
        protected override CodeRefactoringProvider CreateProvider() => new UpdateLambdaParameterRefactoring();

        [Test]
        public void should_be_able_to_update_simple_lambda_parameter()
        {
            TestCodeRefactoring(_001_SimpleLambda, _001_SimpleLambda_FIXED);
        }

        [Test]
        public void should_be_able_to_update_lambda_parameter_when_wrapped_in_parenthesis()
        {
            TestCodeRefactoring(_002_ParenthesisLambda, _002_ParenthesisLambda_FIXED);
        }
        
        [Test]
        public void should_be_able_to_update_generic_lambda_parameter()
        {
            TestCodeRefactoring(_003_GenericLambda, _003_GenericLambda_FIXED);
        }
    }
}
