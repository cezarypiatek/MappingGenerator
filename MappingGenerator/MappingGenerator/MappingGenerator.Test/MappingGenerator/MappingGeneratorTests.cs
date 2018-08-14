using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using NUnit.Framework;
using RoslynNUnitLight;
using TestHelper;
using static MappingGenerator.Test.Helpers.DiagnosticHelper;
using static MappingGenerator.Test.MappingGenerator.MappingGeneratorTestCases;

namespace MappingGenerator.Test.Mapping
{
    public class MappingGeneratorTests:  CodeRefactoringTestFixture
    {
        [Test]
        public void should_be_able_to_generate_pure_mapping_method()
        {
            TestCodeRefactoring(_001_PureMappingMethod, _001_PureMappingMethod_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_pure_mapping_method_for_generic_types()
        {
            TestCodeRefactoring(_002_PureMappingMethodWithGenerics, _002_PureMappingMethodWithGenerics_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_mapping_from_one_parameter_to_another()
        {
            TestCodeRefactoring(_003_MappingFromOneToAnotherParameter, _003_MappingFromOneToAnotherParameter_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_update_this_object_function_with_single_parameter()
        {
            TestCodeRefactoring(_004_UpdateThisObjectWithSingleParameter, _004_UpdateThisObjectWithSingleParameter_FIXED);
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
            DiagnosticResultLocation[] locations = LocationFromTestFile(25, 17);
            TestCodeRefactoring(_006_ConstructorWithSingleParameter, _006_ConstructorWithSingleParameter_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_mapping_constructor_with_multiple_parameters()
        {
            DiagnosticResultLocation[] locations = LocationFromTestFile(25, 17);
            TestCodeRefactoring(_007_ConstructorWithMultipleParameters, _007_ConstructorWithMultipleParameters_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_mapping_for_recursive_types()
        {
            DiagnosticResultLocation[] locations = LocationFromTestFile(11, 25);
            TestCodeRefactoring(_008_StopRecursingMapping, _008_StopRecursingMapping_Fixed);
        }


        protected override string LanguageName => LanguageNames.CSharp;

        protected override CodeRefactoringProvider CreateProvider()
        {
            return new MappingGeneratorRefactoring();
        }
    }
}