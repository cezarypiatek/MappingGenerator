using System;
using System.Diagnostics;

namespace MappingGenerator.OnBuildGenerator
{
    [AttributeUsage(AttributeTargets.Interface)]
    [Conditional("CodeGeneration")]
    public class MappingInterface:Attribute
    {
        
    }
}
