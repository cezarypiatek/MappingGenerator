using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;
using static MappingGenerator.Test.Helpers.DiagnosticHelper;
using static MappingGenerator.Test.MappingGenerator.MappingGeneratorTestCases;

namespace MappingGenerator.Test.Mapping
{
    [TestClass]
    public class MappingGeneratorTests: CodeFixVerifier
    {
        [TestMethod]
        public void should_be_able_to_generate_pure_mapping_method()
        {
            VerifyMapper(_001_PureMappingMethod, _001_PureMappingMethod_FIXED, LocationFromTestFile(10, 31));
        }

        [TestMethod]
        public void should_be_able_to_generate_pure_mapping_method_for_generic_types()
        {
            VerifyMapper(_002_PureMappingMethodWithGenerics, _002_PureMappingMethodWithGenerics_FIXED, LocationFromTestFile(10, 28));
        }

        [TestMethod]
        public void should_be_able_to_generate_mapping_from_one_parameter_to_another()
        {
            VerifyMapper(_003_MappingFromOneToAnotherParameter, _003_MappingFromOneToAnotherParameter_FIXED, LocationFromTestFile(10, 28));
        }

        [TestMethod]
        public void should_be_able_to_generate_update_this_object_function_with_single_parameter()
        {
            VerifyMapper(_004_UpdateThisObjectWithSingleParameter, _004_UpdateThisObjectWithSingleParameter_FIXED, LocationFromTestFile(25, 21));
        }

        //TODO: Function not implemented yet
        //[TestMethod]
        //public void should_be_able_to_generate_update_this_object_function_with_multiple_parameters()
        //{
        //    VerifyMapper(_005_UpdateThisObjectWithMultipleParameters, _005_UpdateThisObjectWithMultipleParameters_FIXED, LocationFromTestFile(22, 13));
        //}

        [TestMethod]
        public void should_be_able_to_generate_mapping_constructor_with_single_parameter()
        {
            VerifyMapper(_006_ConstructorWithSingleParameter, _006_ConstructorWithSingleParameter_FIXED, LocationFromTestFile(25, 17));
        }

        [TestMethod]
        public void should_be_able_to_generate_mapping_constructor_with_multiple_parameters()
        {
            VerifyMapper(_007_ConstructorWithMultipleParameters, _007_ConstructorWithMultipleParameters_FIXED, LocationFromTestFile(25, 17));
        }

        private void VerifyMapper(string test, string fixtest, DiagnosticResultLocation[] locations)
        {
            var expected = new DiagnosticResult
            {
                Id = MappingGeneratorAnalyzer.DiagnosticId,
                Message = MappingGeneratorAnalyzer.MessageFormat.ToString(),
                Severity = DiagnosticSeverity.Info,
                Locations = locations
            };
            VerifyCSharpDiagnostic(test, expected);
            VerifyCSharpFix(test, fixtest, allowNewCompilerDiagnostics: true);
            
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new MappingGeneratorCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new MappingGeneratorAnalyzer();
        }
    }
}