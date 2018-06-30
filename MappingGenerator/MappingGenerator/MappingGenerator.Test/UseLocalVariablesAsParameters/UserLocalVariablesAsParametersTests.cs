using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;
using MappingGenerator.Test.Splatting;
using MappingGenerator.Test.UseLocalVariablesAsParameters;

namespace MappingGenerator.Test
{
    [TestClass]
    public class UseLocalVariablesAsParameter : CodeFixVerifier
    {
        [TestMethod]
        public void should_be_able_to_complete_method_invocation_with_local_variables_as_regular_parameters()
        {
            VerifyCSharpFix(UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocation, UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocation_Fixed, UseLocalVariablesAsParameterCodeFixProvider.CS7036, 0);
        }
        
        [TestMethod]
        public void should_be_able_to_complete_method_invocation_with_local_variables_as_named_parameters()
        {
            VerifyCSharpFix(UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocation, UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocationWithNamedParameters_Fixed, UseLocalVariablesAsParameterCodeFixProvider.CS7036, 1);
        }

        [TestMethod]
        public void should_be_able_to_complete_constructor_invocation_with_local_variables_as_regular_parameters()
        {
            VerifyCSharpFix(UseLocalVariablesTestCases._002_UserLocaVariablesToCompleteConstructorInvocation, UseLocalVariablesTestCases._002_UserLocaVariablesToCompleteConstructorInvocation_Fixed, SplattingCodeFixProvider.CS7036, 0);
        }

        [TestMethod]
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
