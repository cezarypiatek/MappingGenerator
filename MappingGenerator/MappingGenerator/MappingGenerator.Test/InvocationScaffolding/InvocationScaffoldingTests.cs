using MappingGenerator.Features.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using NUnit.Framework;
using RoslynTestKit;
using static MappingGenerator.Test.InvocationScaffolding.InvocationScaffoldingTestCases;

namespace MappingGenerator.Test.InvocationScaffolding
{
    public class InvocationScaffoldingTests: CodeFixTestFixture
    {
        [Test]
        public void should_be_able_to_scaffold_method_invocation_using_regular_arguments()
        {
           TestCodeFix(_001_MethodInvocationScaffolding_With_RegularArguments, _001_MethodInvocationScaffolding_With_RegularArguments_FIXED, InvocationScaffoldingCodeFixProvider.CS7036);
        }

        [Test]
        public void should_be_able_to_scaffold_method_invocation_using_named_arguments()
        {
            TestCodeFix(_002_MethodInvocationScaffolding_With_NamedArguments, _002_MethodInvocationScaffolding_With_NamedArguments_FIXED, InvocationScaffoldingCodeFixProvider.CS7036, 1);
        }

        [Test]
        public void should_be_able_to_scaffold_constructor_invocation_using_regular_arguments()
        {
            TestCodeFix(_003_ConstructorInvocationScaffolding_With_RegularArguments, _003_ConstructorInvocationScaffolding_With_RegularArguments_FIXED, InvocationScaffoldingCodeFixProvider.CS7036);

        }

        [Test]
        public void should_be_able_to_scaffold_constructor_invocation_using_named_arguments()
        {
            TestCodeFix(_004_ConstructorInvocationScaffolding_With_NamedArguments, _004_ConstructorInvocationScaffolding_With_NamedArguments_FIXED, InvocationScaffoldingCodeFixProvider.CS7036, 1);
        }

        protected override string LanguageName => LanguageNames.CSharp;

        protected override CodeFixProvider CreateProvider()
        {
            return new InvocationScaffoldingCodeFixProvider();
        }
    }
}
