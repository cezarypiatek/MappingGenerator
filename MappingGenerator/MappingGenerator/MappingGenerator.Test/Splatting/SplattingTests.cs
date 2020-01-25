using MappingGenerator.Features.CodeFixes;
using Microsoft.CodeAnalysis.CodeFixes;
using MappingGenerator.Test.Splatting;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using RoslynTestKit;

namespace MappingGenerator.Test
{
    public class SplattingTests : CodeFixTestFixture
    {
        [Test]
        public void should_be_able_to_generate_splatting_for_method_invocation_with_regular_parameters()
        {
            TestCodeFix(SplattingTestCases._001_SplattingInMethodInvocation, SplattingTestCases._001_SplattingInMethodInvocation_FIXED, SplattingCodeFixProvider.CS7036, 0);
        }

        [Test]
        public void should_be_able_to_generate_splatting_for_method_invocation_with_named_parameters()
        {
            TestCodeFix(SplattingTestCases._001_SplattingInMethodInvocation, SplattingTestCases._001_SplattingInMethodInvocationWithNamedParameters_FIXED, SplattingCodeFixProvider.CS7036, 1);
        }

        [Test]
        public void should_be_able_to_generate_splatting_for_constructor_invocation_with_regular_parameters()
        {
            TestCodeFix(SplattingTestCases._002_SplattingInConstructorInvocation, SplattingTestCases._002_SplattingInConstructorInvocation_FIXED, SplattingCodeFixProvider.CS7036, 0);
        }

        [Test]
        public void should_be_able_to_generate_splatting_for_constructor_invocation_with_named_parameters()
        {
            TestCodeFix(SplattingTestCases._002_SplattingInConstructorInvocation, SplattingTestCases._002_SplattingInConstructorInvocationWithNamedParameters_FIXED, SplattingCodeFixProvider.CS7036, 1);
        }

        [Test]
        public void should_be_able_to_generate_splatting_for_best_method_overload()
        {
            TestCodeFix(SplattingTestCases._003_SplattingWithBestOverloadMatch, SplattingTestCases._003_SplattingWithBestOverloadMatch_FIXED, SplattingCodeFixProvider.CS1501, 1);
        }

        protected override string LanguageName => LanguageNames.CSharp;
        protected override CodeFixProvider CreateProvider()
        {
            return new SplattingCodeFixProvider();
        }
    }
}
