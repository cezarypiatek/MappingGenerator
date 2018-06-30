using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;
using MappingGenerator.Test.Splatting;

namespace MappingGenerator.Test
{
    [TestClass]
    public class SplattingTests : CodeFixVerifier
    {
        [TestMethod]
        public void should_be_able_to_generate_splatting_for_method_invocation_with_regular_parameters()
        {
            VerifyCSharpFix(SplattingTestCases._001_SplattingInMethodInvocation, SplattingTestCases._001_SplattingInMethodInvocation_FIXED, SplattingCodeFixProvider.CS7036, 0);
        }

        [TestMethod]
        public void should_be_able_to_generate_splatting_for_method_invocation_with_named_parameters()
        {
            VerifyCSharpFix(SplattingTestCases._001_SplattingInMethodInvocation, SplattingTestCases._001_SplattingInMethodInvocationWithNamedParameters_FIXED, SplattingCodeFixProvider.CS7036, 1);
        }

        [TestMethod]
        public void should_be_able_to_generate_splatting_for_constructor_invocation_with_regular_parameters()
        {
            VerifyCSharpFix(SplattingTestCases._002_SplattingInConstructorInvocation, SplattingTestCases._002_SplattingInConstructorInvocation_FIXED, SplattingCodeFixProvider.CS7036, 0);
        }
        
        [TestMethod]
        public void should_be_able_to_generate_splatting_for_constructor_invocation_with_named_parameters()
        {
            VerifyCSharpFix(SplattingTestCases._002_SplattingInConstructorInvocation, SplattingTestCases._002_SplattingInConstructorInvocationWithNamedParameters_FIXED, SplattingCodeFixProvider.CS7036, 1);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new SplattingCodeFixProvider();
        }
    }
}
