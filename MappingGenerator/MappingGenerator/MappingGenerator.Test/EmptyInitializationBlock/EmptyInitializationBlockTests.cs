using MappingGenerator.Test.EmptyInitializationBlock;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using NUnit.Framework;
using RoslynTestKit;


namespace MappingGenerator.Test
{
    public class EmptyInitializationBlockTests : CodeRefactoringTestFixture
    {
        [Test]
        public void should_be_able_to_generate_initialization_block_with_local_variables()
        {
            var test = EmptyInitializationBlockTestCases._001_CompleteInitializationBlockWithLocals;
            var fixedCode = EmptyInitializationBlockTestCases._001_CompleteInitializationBlockWithLocals_FIXED;
            TestCodeRefactoring(test, fixedCode);
        }

        [Test]
        public void should_be_able_to_generate_initialization_block_with_from_lambda_parameter()
        {
            var test = EmptyInitializationBlockTestCases._002_CompleteInitializationBlockWithLambdaParameter;
            var fixedCode = EmptyInitializationBlockTestCases._002_CompleteInitializationBlockWithLambdaParameter_FIXED;
            TestCodeRefactoring(test, fixedCode, 2);
        }

        [Test]
        public void should_be_able_to_generate_initialization_block_from_simple_lambda_parameter()
        {
            var test = EmptyInitializationBlockTestCases._003_CompleteInitializationBlockWithSompleLambdaParameter;
            var fixedCode = EmptyInitializationBlockTestCases._003_CompleteInitializationBlockWithSompleLambdaParameter_FIXED;
            TestCodeRefactoring(test, fixedCode, 2);
        }

        [Test]
        public void should_be_able_to_generate_initialization_block_with_sample_data_for_recursive_type()
        {
            var test = EmptyInitializationBlockTestCases._004_CompleteInitializationBlockWithSampleDataRecursiveType;
            var fixedCode = EmptyInitializationBlockTestCases._004_CompleteInitializationBlockWithSampleDataRecursiveType_FIXED;
            TestCodeRefactoring(test, fixedCode, 1);
        }

        [Test]
        public void should_be_able_to_generate_initialization_block_with_sample_data_for_complex_type()
        {
            var test = EmptyInitializationBlockTestCases._005_CompleteInitializationBlockWithSampleDatComplexType;
            var fixedCode = EmptyInitializationBlockTestCases._005_CompleteInitializationBlockWithSampleDatComplexType_FIXED;
            TestCodeRefactoring(test, fixedCode, 1);
        }
      
        protected override string LanguageName => LanguageNames.CSharp;
        protected override CodeRefactoringProvider CreateProvider()
        {
            return new EmptyInitializationBlockRefactoring();
        }
    }
}
