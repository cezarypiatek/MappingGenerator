using System;
using System.Collections.Generic;

namespace MappingGenerator.Test.MappingGenerator.TestCaseData
{
    public class Model1Vm
    {
        public int Id { get; set; }
        public List<Model2Vm> Model2s { get; set; }

        public Model1Vm Map(Model1 src)
        {
            throw new NotImplementedException();
        }
    }

    public class Model2Vm
    {
        public int Id { get; set; }
        public Model1Vm Model1 { get; set; }

        public string Label { get; set; }
    }

    public class Model1
    {
        public int Id { get; set; }
        public List<Model2> Model2s { get; set; }
    }

    public class Model2
    {
        public int Id { get; set; }
        public Model1 Model1 { get; set; }

        public string Label { get; set; }
    }
}