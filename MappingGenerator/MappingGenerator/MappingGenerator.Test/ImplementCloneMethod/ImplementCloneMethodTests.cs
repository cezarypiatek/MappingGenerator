using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using NUnit.Framework;
using RoslynNUnitLight;

namespace MappingGenerator.Test.ImplementCloneMethod
{
    public class ImplementCloneMethodTests :  CodeRefactoringTestFixture
    {
        [Test]
        public void should_be_able_to_generate_deep_clone_method()
        {
            var test = ImplementCloneMethodTestCases._001_DeepClone;
            var fixedCode = ImplementCloneMethodTestCases._001_DeepClone_FIXED;
            TestCodeRefactoring(test, fixedCode);
        }

        protected override string LanguageName => LanguageNames.CSharp;
        protected override CodeRefactoringProvider CreateProvider()
        {
            return new ImplementCloneMethodRefactoring();
        }
    }
}
