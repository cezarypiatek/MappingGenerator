using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.Mappings
{
    public class MappingContext
    {
        public HashSet<MappingType> MissingConversions { get; } = new HashSet<MappingType>();

        public void AddMissingConversion(ITypeSymbol fromType, ITypeSymbol toType) => MissingConversions.Add(
            new MappingType()
            {
                FromType = fromType,
                ToType = toType
            });

        public bool WrapInCustomConversion { get; set; }
    }
}