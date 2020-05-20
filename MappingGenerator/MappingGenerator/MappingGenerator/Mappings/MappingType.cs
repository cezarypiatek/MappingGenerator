using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.Mappings
{
    public class MappingType
    {
        public ITypeSymbol FromType { get; set; }
        public ITypeSymbol ToType { get; set; }

        public override bool Equals(object obj)
        {
            return obj is MappingType type &&
                   EqualityComparer<ITypeSymbol>.Default.Equals(FromType, type.FromType) &&
                   EqualityComparer<ITypeSymbol>.Default.Equals(ToType, type.ToType);
        }

        public override int GetHashCode()
        {
            int hashCode = 1267219665;
            hashCode = hashCode * -1521134295 + EqualityComparer<ITypeSymbol>.Default.GetHashCode(FromType);
            hashCode = hashCode * -1521134295 + EqualityComparer<ITypeSymbol>.Default.GetHashCode(ToType);
            return hashCode;
        }
    }
}