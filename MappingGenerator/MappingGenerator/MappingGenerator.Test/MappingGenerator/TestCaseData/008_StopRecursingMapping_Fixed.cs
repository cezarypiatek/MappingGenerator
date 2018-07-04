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
            return new Model1Vm()
            {
                Id = src.Id,
                Model2s = src.Model2s.Select(srcModel2 => new Model2Vm()
                {
                    Id = srcModel2.Id,
                    Model1 = srcModel2.Model1 /* Stop recursing mapping */,
                    Label = srcModel2.Label
                }).ToList()
            };
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