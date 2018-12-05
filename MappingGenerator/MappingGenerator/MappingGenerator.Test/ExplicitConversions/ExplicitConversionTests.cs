using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using NUnit.Framework;
using RoslynNUnitLight;

namespace MappingGenerator.Test.ExplicitConversions
{
    public class ExplicitConversionTests: CodeFixTestFixture
    {
        [Test]
        public void should_be_able_to_generate_conversion_for_invalid_assignment()
        {
            TestCodeFix(ExplicitConversionTestCases._001_ExplicitConversionForInvalidAssigment, ExplicitConversionTestCases._001_ExplicitConversionForInvalidAssigment_FIXED, ExplicitConversionCodeFixProvider.CS0029, 0);

        }

        [Test]
        public void should_be_able_to_generate_conversion_for_invalid_return_statement()
        {
            TestCodeFix(ExplicitConversionTestCases._002_ExplicitConversionForInvalidReturn, ExplicitConversionTestCases._002_ExplicitConversionForInvalidReturn_FIXED, ExplicitConversionCodeFixProvider.CS0266, 0);

        }

        [Test]
        public void should_be_able_to_generate_conversion_for_invalid_yield_statement()
        {
            TestCodeFix(ExplicitConversionTestCases._003_ExplicitConversionForInvalidYield, ExplicitConversionTestCases._003_ExplicitConversionForInvalidYield_FIXED, ExplicitConversionCodeFixProvider.CS0029, 0);

        }

        [Test]
        public void should_be_able_to_generate_conversion_for_invalid_assignment_statement_in_init_block()
        {
            TestCodeFix(ExplicitConversionTestCases._004_ExplicitConversionForInvalidAssigmentInInitBlock, ExplicitConversionTestCases._004_ExplicitConversionForInvalidAssigmentInInitBlock_FIXED, ExplicitConversionCodeFixProvider.CS0029, 0);
        }
        protected override string LanguageName => LanguageNames.CSharp;

        protected override CodeFixProvider CreateProvider()
        {
            return new ExplicitConversionCodeFixProvider();
        }
    }
}
