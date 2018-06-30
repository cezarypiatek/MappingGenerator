using System;
using System.Collections.Generic;
using System.Text;

namespace MappingGenerator.Test.UseLocalVariablesAsParameters.TestCaseData
{
    public class SampleClass
    {
        public void DoSomething(string firstName, string lastName)
        {
            var age = 33;
            var weight = 80m;
            DoSomethingElse();
        }

        public void DoSomethingElse(string firstName, string lastName, int age, decimal weight)
        {

        }
    }
}