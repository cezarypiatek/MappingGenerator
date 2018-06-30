using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace MappingGenerator.Test.ExplicitConversions
{
    [TestClass]
    public class ExplicitConversionTests: CodeFixVerifier
    {
        [TestMethod]
        public void should_be_able_to_generate_conversion_for_invalid_assigment()
        {
            VerifyCSharpFix(ExplicitConversionTestCases._001_ExplicitConversionForInvalidAssigment, ExplicitConversionTestCases._001_ExplicitConversionForInvalidAssigment_FIXED, ExplicitConversionCodeFixProvider.CS0029, 0);

        }
        
        [TestMethod]
        public void should_be_able_to_generate_conversion_for_invalid_return_statement()
        {
            VerifyCSharpFix(ExplicitConversionTestCases._002_ExplicitConversionForInvalidReturn, ExplicitConversionTestCases._002_ExplicitConversionForInvalidReturn_FIXED, ExplicitConversionCodeFixProvider.CS0266, 0);

        }
        
        [TestMethod]
        public void should_be_able_to_generate_conversion_for_invalid_yield_statement()
        {
            VerifyCSharpFix(ExplicitConversionTestCases._003_ExplicitConversionForInvalidYield, ExplicitConversionTestCases._003_ExplicitConversionForInvalidYield_FIXED, ExplicitConversionCodeFixProvider.CS0029, 0);

        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new ExplicitConversionCodeFixProvider();
        }
    }
}
