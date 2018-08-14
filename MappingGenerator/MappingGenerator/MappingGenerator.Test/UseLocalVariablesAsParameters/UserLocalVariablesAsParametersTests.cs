using Microsoft.CodeAnalysis.CodeFixes;
using TestHelper;
using MappingGenerator.Test.UseLocalVariablesAsParameters;
using NUnit.Framework;

namespace MappingGenerator.Test
{
    
    public class UseLocalVariablesAsParameter : CodeFixVerifier
    {
        [Test]
        public void should_be_able_to_complete_method_invocation_with_local_variables_as_regular_parameters()
        {
            VerifyCSharpFix(UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocation, UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocation_Fixed, UseLocalVariablesAsParameterCodeFixProvider.CS7036, 0);
        }

        [Test]
        public void should_be_able_to_complete_method_invocation_with_local_variables_as_named_parameters()
        {
            VerifyCSharpFix(UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocation, UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocationWithNamedParameters_Fixed, UseLocalVariablesAsParameterCodeFixProvider.CS7036, 1);
        }

        [Test]
        public void should_be_able_to_complete_constructor_invocation_with_local_variables_as_regular_parameters()
        {
            VerifyCSharpFix(UseLocalVariablesTestCases._002_UserLocaVariablesToCompleteConstructorInvocation, UseLocalVariablesTestCases._002_UserLocaVariablesToCompleteConstructorInvocation_Fixed, SplattingCodeFixProvider.CS7036, 0);
        }

        [Test]
        public void should_be_able_to_complete_constructor_invocation_with_local_variables_as_named_parameters()
        {
            VerifyCSharpFix(UseLocalVariablesTestCases._002_UserLocaVariablesToCompleteConstructorInvocation, UseLocalVariablesTestCases._002_UserLocaVariablesToCompleteConstructorInvocationWithNamedParameters_Fixed, SplattingCodeFixProvider.CS7036, 1);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new UseLocalVariablesAsParameterCodeFixProvider();
        }
    }
}
