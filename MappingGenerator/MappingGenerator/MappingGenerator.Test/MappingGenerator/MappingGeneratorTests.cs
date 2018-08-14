using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using TestHelper;
using static MappingGenerator.Test.Helpers.DiagnosticHelper;
using static MappingGenerator.Test.MappingGenerator.MappingGeneratorTestCases;

namespace MappingGenerator.Test.Mapping
{
    public class MappingGeneratorTests: CodeFixVerifier
    {
        [Test]
        public void should_be_able_to_generate_pure_mapping_method()
        {
            VerifyMapper(_001_PureMappingMethod, _001_PureMappingMethod_FIXED, LocationFromTestFile(10, 31));
        }

        [Test]
        public void should_be_able_to_generate_pure_mapping_method_for_generic_types()
        {
            VerifyMapper(_002_PureMappingMethodWithGenerics, _002_PureMappingMethodWithGenerics_FIXED, LocationFromTestFile(10, 28));
        }

        [Test]
        public void should_be_able_to_generate_mapping_from_one_parameter_to_another()
        {
            VerifyMapper(_003_MappingFromOneToAnotherParameter, _003_MappingFromOneToAnotherParameter_FIXED, LocationFromTestFile(10, 28));
        }

        [Test]
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

        [Test]
        public void should_be_able_to_generate_mapping_constructor_with_single_parameter()
        {
            VerifyMapper(_006_ConstructorWithSingleParameter, _006_ConstructorWithSingleParameter_FIXED, LocationFromTestFile(25, 17));
        }

        [Test]
        public void should_be_able_to_generate_mapping_constructor_with_multiple_parameters()
        {
            VerifyMapper(_007_ConstructorWithMultipleParameters, _007_ConstructorWithMultipleParameters_FIXED, LocationFromTestFile(25, 17));
        }

        [Test]
        public void should_be_able_to_generate_mapping_for_recursive_types()
        {
            VerifyMapper(_008_StopRecursingMapping, _008_StopRecursingMapping_Fixed, LocationFromTestFile(11, 25));
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