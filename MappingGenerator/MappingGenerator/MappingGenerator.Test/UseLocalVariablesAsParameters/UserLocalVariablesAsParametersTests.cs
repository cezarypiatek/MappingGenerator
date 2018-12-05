using Microsoft.CodeAnalysis.CodeFixes;
using TestHelper;
using MappingGenerator.Test.UseLocalVariablesAsParameters;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using RoslynNUnitLight;

namespace MappingGenerator.Test
{
    
    public class UseLocalVariablesAsParameter : CodeFixTestFixture
    {
        [Test]
        public void should_be_able_to_complete_method_invocation_with_local_variables_as_regular_parameters()
        {
            TestCodeFix(UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocation, UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocation_Fixed, UseLocalVariablesAsParameterCodeFixProvider.CS7036, 0);
        }

        [Test]
        public void should_be_able_to_complete_method_invocation_with_local_variables_as_named_parameters()
        {
            TestCodeFix(UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocation, UseLocalVariablesTestCases._001_UserLocaVariablesToCompleteMethodInvocationWithNamedParameters_Fixed, UseLocalVariablesAsParameterCodeFixProvider.CS7036, 1);
        }

        [Test]
        public void should_be_able_to_complete_constructor_invocation_with_local_variables_as_regular_parameters()
        {
            TestCodeFix(UseLocalVariablesTestCases._002_UserLocaVariablesToCompleteConstructorInvocation, UseLocalVariablesTestCases._002_UserLocaVariablesToCompleteConstructorInvocation_Fixed, SplattingCodeFixProvider.CS7036, 0);
        }

        [Test]
        public void should_be_able_to_complete_constructor_invocation_with_local_variables_as_named_parameters()
        {
            TestCodeFix(UseLocalVariablesTestCases._002_UserLocaVariablesToCompleteConstructorInvocation, UseLocalVariablesTestCases._002_UserLocaVariablesToCompleteConstructorInvocationWithNamedParameters_Fixed, SplattingCodeFixProvider.CS7036, 1);
        }
        
        protected override string LanguageName => LanguageNames.CSharp;

        protected override CodeFixProvider CreateProvider()
        {
            return new UseLocalVariablesAsParameterCodeFixProvider();
        }
    }
}
