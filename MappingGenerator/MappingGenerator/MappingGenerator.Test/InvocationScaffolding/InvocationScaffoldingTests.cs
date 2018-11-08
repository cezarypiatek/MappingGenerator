using Microsoft.CodeAnalysis.CodeFixes;
using NUnit.Framework;
using TestHelper;
using static MappingGenerator.Test.InvocationScaffolding.InvocationScaffoldingTestCases;

namespace MappingGenerator.Test.InvocationScaffolding
{
    public class InvocationScaffoldingTests: CodeFixVerifier
    {
        [Test]
        public void should_be_able_to_scaffold_method_invocation_using_regular_arguments()
        {
            VerifyCSharpFix(_001_MethodInvocationScaffolding_With_RegularArguments, _001_MethodInvocationScaffolding_With_RegularArguments_FIXED, InvocationScaffoldingCodeFixProvider.CS7036);
        }

        [Test]
        public void should_be_able_to_scaffold_method_invocation_using_named_arguments()
        {
            VerifyCSharpFix(_002_MethodInvocationScaffolding_With_NamedArguments, _002_MethodInvocationScaffolding_With_NamedArguments_FIXED, InvocationScaffoldingCodeFixProvider.CS7036, 1);
        }

        [Test]
        public void should_be_able_to_scaffold_constructor_invocation_using_regular_arguments()
        {
            VerifyCSharpFix(_003_ConstructorInvocationScaffolding_With_RegularArguments, _003_ConstructorInvocationScaffolding_With_RegularArguments_FIXED, InvocationScaffoldingCodeFixProvider.CS7036);

        }

        [Test]
        public void should_be_able_to_scaffold_constructor_invocation_using_named_arguments()
        {
            VerifyCSharpFix(_004_ConstructorInvocationScaffolding_With_NamedArguments, _004_ConstructorInvocationScaffolding_With_NamedArguments_FIXED, InvocationScaffoldingCodeFixProvider.CS7036, 1);

        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
           return new InvocationScaffoldingCodeFixProvider();
        }
    }
}
