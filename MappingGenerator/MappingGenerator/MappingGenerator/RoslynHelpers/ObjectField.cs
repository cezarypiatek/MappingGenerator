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

        public ISymbol UnderlyingSymbol => fieldSymbol;
        public bool CanBeSetPublicly(IAssemblySymbol contextAssembly)
        {
            throw new System.NotImplementedException();
        }

        public bool CanBeSetPrivately(ITypeSymbol fromType)
        {
            throw new System.NotImplementedException();
        }

        public bool CanBeSetInConstructor(ITypeSymbol fromType)
        {
            throw new System.NotImplementedException();
        }
    }
}