using MappingGenerator.Mappings;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.RoslynHelpers
{
    public class ObjectField : IObjectField
    {
        private readonly IFieldSymbol fieldSymbol;

        public ObjectField(IFieldSymbol fieldSymbol)
        {
            this.fieldSymbol = fieldSymbol;
        }

        public string Name => fieldSymbol.Name;

        public ITypeSymbol Type => fieldSymbol.Type;

        public bool CanBeSet(ITypeSymbol via, MappingContext mappingContext)
        {
            throw new System.NotImplementedException();
        }

        public bool CanBeSetInConstructor(ITypeSymbol via, MappingContext mappingContext)
        {
            throw new System.NotImplementedException();
        }

        public bool CanBeGet(ITypeSymbol via, MappingContext mappingContext)
        {
            throw new System.NotImplementedException();
        }
    }
}