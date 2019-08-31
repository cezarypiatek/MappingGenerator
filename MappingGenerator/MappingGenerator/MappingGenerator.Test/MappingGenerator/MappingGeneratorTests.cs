using System.Collections.Immutable;
using MappingGenerator.Features.Refactorings.Mapping;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using NUnit.Framework;
using RoslynTestKit;
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
            TestCodeRefactoring(_006_ConstructorWithSingleParameter, _006_ConstructorWithSingleParameter_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_mapping_constructor_with_multiple_parameters()
        {
            TestCodeRefactoring(_007_ConstructorWithMultipleParameters, _007_ConstructorWithMultipleParameters_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_mapping_for_recursive_types()
        {
            TestCodeRefactoring(_008_StopRecursingMapping, _008_StopRecursingMapping_Fixed);
        }

        [Test]
        public void should_be_able_to_generate_identity_mapping_function()
        {
            TestCodeRefactoring(_009_IdentityFunctionMapping, _009_IdentityFunctionMapping_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_identity_mapping_function_for_simple_type()
        {
            TestCodeRefactoring(_010_IdentityFunctionMappingForSimpleType, _010_IdentityFunctionMappingForSimpleType_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_identity_mapping_function_for_collections()
        {
            TestCodeRefactoring(_011_IdentityFunctionMappingForCollection, _011_IdentityFunctionMappingForCollection_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_collection_mapping_with_lambda_parameter_name_in_singular_form()
        {
            TestCodeRefactoring(_012_CollectionMappingWithSingularLambdaParameterName, _012_CollectionMappingWithSingularLambdaParameterName_FIXED);
        }


        [Test]
        public void should_be_able_to_generate_collection_mapping_with_lambda_parameter_name_with_variable_name_as_prefix()
        {
            TestCodeRefactoring(_013_CollectionMappingWithPrefixedLambdaParameterName, _013_CollectionMappingWithPrefixedLambdaParameterName_FIXED_);
        }


        [Test]
        public void should_be_able_to_generate_collection_mapping_with_lambda_parameter_from_generic_name()
        {
            TestCodeRefactoring(_014_CollectionMappingWithGenericName, _014_CollectionMappingWithGenericName_FIXED);
        }


        [Test]
        public void should_be_able_to_generate_collection_mapping_with_lambda_parameter_from_postfiex_generic_name()
        {
            TestCodeRefactoring(_015_CollectionMappingWithPostfixGenericName, _015_CollectionMappingWithPostfixGenericName_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_collection_mapping_with_lambda_parameter_with_irregular_noun()
        {
            TestCodeRefactoring(_016_CollectionMappingWithIrregularSingularLambdaParameterName, _016_CollectionMappingWithIrregularSingularLambdaParameterName_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_collection_mapping_with_lambda_parameter_with_irregular_camelcase_noun()
        {
            TestCodeRefactoring(_017_CollectionMappingWithIrregularCamelCaseSingularLambdaParameterName, _017_CollectionMappingWithIrregularCamelCaseSingularLambdaParameterName_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_collection_mapping_with_lambda_parameter_from_postfiex_generic_name_singular()
        {
            TestCodeRefactoring(_018_CollectionMappingWithPostfixGenericNameInSingular, _018_CollectionMappingWithPostfixGenericNameInSingular_FIXED);
        }

        [Test]
        public void should_be_able_to_map_properties_inherited_from_class_declared_in_external_library()
        {
            TestCodeRefactoring(_019_MappingPropertiesInheritedFromLibraryClass, _019_MappingPropertiesInheritedFromLibraryClass_FIXED);
        }

        [Test]
        public void should_be_able_to_map_properties_inside_constructor_inherited_from_class_declared_in_external_library()
        {
            TestCodeRefactoring(_020_MappingPropertiesInConstructorInheritedFromLibraryClass, _020_MappingPropertiesInConstructorInheritedFromLibraryClass_FIXED);
        }

        [Test]
        public void should_be_able_to_implement_multi_parameter_pure_method()
        {
            TestCodeRefactoring(_021_MultiParameterPureMappingMethod, _021_MultiParameterPureMappingMethod_FIxed);
        }

        [Test]
        public void should_be_able_to_map_IList_to_List_using_linq()
        {
            TestCodeRefactoring(_022_CollectionMappingIListToList, _022_CollectionMappingIListToList_Fixed);
        }

        [Test]
        public void should_be_able_to_replace_lambda_body_with_mapping_body()
        {
            TestCodeRefactoring(_023_PureLambdaMappingMethod, _023_PureLambdaMappingMethod_FIXED);
        }

        [Test]
        public void should_be_able_to_replace_invalid_body_with_mapping_body()
        {
            TestCodeRefactoring(_024_InvalidSyntaxPureMappingMethod, _024_InvalidSyntaxPureMappingMethod_FIXED);
        }


        [Test]
        public void should_be_able_to_convert_this_object_to_other()
        {
            TestCodeRefactoring(_025_ThisObjectToOtherMapping, _025_ThisObjectToOtherMapping_FIXED);
        }

        protected override string LanguageName => LanguageNames.CSharp;

        protected override CodeRefactoringProvider CreateProvider()
        {
            return new MappingGeneratorRefactoring();
        }

        protected override ImmutableList<MetadataReference> References { get => new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(IdentityUser).Assembly.Location)
        }.ToImmutableList();}
    }
}