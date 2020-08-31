using MappingGenerator.Mappings.SourceFinders;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.Mappings
{
    public class AnnotatedType
    {
        public ITypeSymbol Type { get; }
        public bool CanBeNull { get; }

        public AnnotatedType(ITypeSymbol type)
        {
            Type = type;
            CanBeNull = type.CanBeNull();
        }

        public AnnotatedType(ITypeSymbol type, bool canBeNull)
        {
            Type = type;
            CanBeNull = canBeNull;
        }

        public AnnotatedType AsNotNull()
        {
            return new AnnotatedType(Type, false);
        }
    }
}