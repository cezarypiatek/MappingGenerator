using MappingGenerator.Features.Refactorings.Mapping;
using MappingGenerator.Test.MappingGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using NUnit.Framework;
using RoslynTestKit;

namespace MappingGenerator.Test.Mapping
{
    public class MappingGeneratorForBrokenCodeTests : CodeRefactoringTestFixture
    {
        protected override string LanguageName => LanguageNames.CSharp;
        protected override CodeRefactoringProvider CreateProvider() => new MappingGeneratorRefactoring();
        protected override bool FailWhenInputContainsErrors => false;

        [Test]
        public void should_be_able_to_replace_invalid_body_with_mapping_body()
        {
            TestCodeRefactoring(MappingGeneratorTestCases._024_InvalidSyntaxPureMappingMethod, MappingGeneratorTestCases._024_InvalidSyntaxPureMappingMethod_FIXED);
        }
    }
}