using Microsoft.CodeAnalysis.CodeFixes;
using TestHelper;
using MappingGenerator.Test.Splatting;
using NUnit.Framework;

namespace MappingGenerator.Test
{
    public class SplattingTests : CodeFixVerifier
    {
        [Test]
        public void should_be_able_to_generate_splatting_for_method_invocation_with_regular_parameters()
        {
            VerifyCSharpFix(SplattingTestCases._001_SplattingInMethodInvocation, SplattingTestCases._001_SplattingInMethodInvocation_FIXED, SplattingCodeFixProvider.CS7036, 0);
        }

        [Test]
        public void should_be_able_to_generate_splatting_for_method_invocation_with_named_parameters()
        {
            VerifyCSharpFix(SplattingTestCases._001_SplattingInMethodInvocation, SplattingTestCases._001_SplattingInMethodInvocationWithNamedParameters_FIXED, SplattingCodeFixProvider.CS7036, 1);
        }

        [Test]
        public void should_be_able_to_generate_splatting_for_constructor_invocation_with_regular_parameters()
        {
            VerifyCSharpFix(SplattingTestCases._002_SplattingInConstructorInvocation, SplattingTestCases._002_SplattingInConstructorInvocation_FIXED, SplattingCodeFixProvider.CS7036, 0);
        }

        [Test]
        public void should_be_able_to_generate_splatting_for_constructor_invocation_with_named_parameters()
        {
            VerifyCSharpFix(SplattingTestCases._002_SplattingInConstructorInvocation, SplattingTestCases._002_SplattingInConstructorInvocationWithNamedParameters_FIXED, SplattingCodeFixProvider.CS7036, 1);
        }

        [Test]
        public void should_be_able_to_generate_splatting_for_best_method_overload()
        {
            VerifyCSharpFix(SplattingTestCases._003_SplattingWithBestOverloadMatch, SplattingTestCases._003_SplattingWithBestOverloadMatch_FIXED, SplattingCodeFixProvider.CS1501, 1);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new SplattingCodeFixProvider();
        }
    }
}
