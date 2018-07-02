using MappingGenerator.Test.EmptyInitializationBlock;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;
using MappingGenerator.Test.Helpers;

namespace MappingGenerator.Test
{
    [TestClass]
    public class EmptyInitializationBlockTests : CodeFixVerifier
    {
        [TestMethod]
        public void should_be_able_to_generate_initialization_block_with_local_variables()
        {
            var test =  EmptyInitializationBlockTestCases._001_CompleteInitializationBlockWithLocals;
            var expected = new DiagnosticResult
            {
                Id = EmptyInitializationBlockAnalyzer.DiagnosticId,
                Message = EmptyInitializationBlockAnalyzer.MessageFormat.ToString(),
                Severity = DiagnosticSeverity.Info,
                Locations = DiagnosticHelper.LocationFromTestFile(22, 13)
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = EmptyInitializationBlockTestCases._001_CompleteInitializationBlockWithLocals_FIXED;
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void should_be_able_to_generate_initialization_block_with_from_lambda_parameter()
        {
            var test =  EmptyInitializationBlockTestCases._002_CompleteInitializationBlockWithLambdaParameter;
            var expected = new DiagnosticResult
            {
                Id = EmptyInitializationBlockAnalyzer.DiagnosticId,
                Message = EmptyInitializationBlockAnalyzer.MessageFormat.ToString(),
                Severity = DiagnosticSeverity.Info,
                Locations = DiagnosticHelper.LocationFromTestFile( 22, 96)
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = EmptyInitializationBlockTestCases._002_CompleteInitializationBlockWithLambdaParameter_FIXED;
            VerifyCSharpFix(test, fixtest, 1);
        }   
        
        [TestMethod]
        public void should_be_able_to_generate_initialization_block_from_simple_lambda_parameter()
        {
            var test =  EmptyInitializationBlockTestCases._003_CompleteInitializationBlockWithSompleLambdaParameter;
            var expected = new DiagnosticResult
            {
                Id = EmptyInitializationBlockAnalyzer.DiagnosticId,
                Message = EmptyInitializationBlockAnalyzer.MessageFormat.ToString(),
                Severity = DiagnosticSeverity.Info,
                Locations = DiagnosticHelper.LocationFromTestFile(12, 68)
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = EmptyInitializationBlockTestCases._003_CompleteInitializationBlockWithSompleLambdaParameter_FIXED;
            VerifyCSharpFix(test, fixtest, 1);
        }
        

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new EmptyInitializationBlockCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new EmptyInitializationBlockAnalyzer();
        }
    }
}
