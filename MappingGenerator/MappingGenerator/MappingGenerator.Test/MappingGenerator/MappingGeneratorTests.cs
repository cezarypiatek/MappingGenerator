using System.Collections.Generic;
using System.Collections.Immutable;
using MappingGenerator.Features.Refactorings;
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
        public void should_be_able_to_generate_update_this_object_function_with_single_parameter_by_decomposition()
        {
            TestCodeRefactoring(_004_UpdateThisObjectWithSingleParameterDecomposition, _004_UpdateThisObjectWithSingleParameterDecomposition_FIXED);
        }


        [Test]
        public void should_be_able_to_generate_update_this_object_function_with_multiple_parameters()
        {
            TestCodeRefactoring(_005_UpdateThisObjectWithMultipleParameters, _005_UpdateThisObjectWithMultipleParameters_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_update_this_object_function_with_two_parameters()
        {
            TestCodeRefactoring(_005_UpdateThisObjectWithTwoParameters, _005_UpdateThisObjectWithTwoParameters_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_mapping_constructor_with_single_parameter()
        {
            TestCodeRefactoring(_006_ConstructorWithSingleParameterDecomposition, _006_ConstructorWithSingleParameterDecomposition_FIXED);
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
        public void should_be_able_to_convert_this_object_to_other()
        {
            TestCodeRefactoring(_025_ThisObjectToOtherMapping, _025_ThisObjectToOtherMapping_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_update_this_object_function_with_single_parameter_by_direct_mapping()
        {
            TestCodeRefactoring(_026_UpdateThisObjectWithSingleParameterMethod, _026_UpdateThisObjectWithSingleParameterMethod_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_constructor_mapping_with_single_parameter_by_direct_mapping()
        {
            TestCodeRefactoring(_027_ConstructorWithSingleParameter, _027_ConstructorWithSingleParameter_FIXED);
        }
        
        [Test]
        public void should_be_able_to_update_using_private_method()
        {
            TestCodeRefactoring(_028_PrivateUpdateFieldAccessibility, _028_PrivateUpdateFieldAccessibility_FIXEDy);
        } 
        
        [Test]
        public void should_be_able_to_update_using_constructor()
        {
            TestCodeRefactoring(_030_ConstructorUpdateFieldAccessibility, _030_ConstructorUpdateFieldAccessibility_FIXED);
        } 
        
        [Test]
        public void should_be_able_to_update_public_fields()
        {
            TestCodeRefactoring(_032_PubliclyUpdateFieldAccessibility, _032_PubliclyUpdateFieldAccessibility_FIXED);
        }

        [Test]
        public void should_be_able_to_generate_mapping_using_custom_conversions()
        {
            TestCodeRefactoring(_029_PureMappingMethodWithCustomConversions, _029_PureMappingMethodWithCustomConversions_FIXED, 1);
        }
        
        [Test]
        public void should_be_able_to_generate_mapping_using_inherited_custom_conversions()
        {
            TestCodeRefactoring(_031_PureMappingMethodWithInheritedCustomConversions, _031_PureMappingMethodWithInheritedCustomConversions_FIXED, 1);
        }
        
        [Test]
        public void should_be_able_to_generate_mapping_using_fields()
        {
            TestCodeRefactoring(_033_PureMappingMethodWithFields, _033_PureMappingMethodWithFields_FIXED, 1);
        }
        
        [Test]
        public void should_be_able_to_map_fields_with_respect_to_accessibility()
        {
            TestCodeRefactoring(_034_PubliclyUpdateFieldAccessibilityForFields, _034_PubliclyUpdateFieldAccessibilityForFields_FIXED, 1);
        }
        
        [Test]
        public void should_be_able_to_initialize_fields_in_constructor()
        {
            TestCodeRefactoring(_035_ConstructorInitializeFIelds, _035_ConstructorInitializeFIelds_FIXED, 1);
        }
        
        [Test]
        public void should_handle_nullable_references()
        {
            TestCodeRefactoring(_036_PureMappingMethodWithNullable, _036_PureMappingMethodWithNullable_FIXED, 1);
        }

        protected override string LanguageName => LanguageNames.CSharp;

        protected override CodeRefactoringProvider CreateProvider()
        {
            return new MappingGeneratorRefactoring();
        }

        protected override IReadOnlyCollection<MetadataReference> References => new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(IdentityUser).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ImmutableArray<>).Assembly.Location), 
        };
    }
}

