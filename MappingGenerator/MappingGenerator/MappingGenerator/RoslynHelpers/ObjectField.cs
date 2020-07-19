using System;
using MappingGenerator.Mappings;
using MappingGenerator.Mappings.SourceFinders;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.RoslynHelpers
{
    public class ObjectField : IObjectField
    {
        private readonly IFieldSymbol fieldSymbol;

        public ObjectField(IFieldSymbol fieldSymbol)
        {
            this.fieldSymbol = fieldSymbol;
            this.Type = new AnnotatedType(fieldSymbol.Type);
        }

        public string Name => fieldSymbol.Name;

        public AnnotatedType Type { get; }

        public bool CanBeSet(ITypeSymbol via, MappingContext mappingContext)
        {
            if (fieldSymbol.IsReadOnly)
            {
                return false;
            }

            return mappingContext.AccessibilityHelper.IsSymbolAccessible(fieldSymbol, via);

        }

        public bool CanBeSetInConstructor(ITypeSymbol via, MappingContext mappingContext)
        {
            return mappingContext.AccessibilityHelper.IsSymbolAccessible(fieldSymbol, via);
        }

        public bool CanBeGet(ITypeSymbol via, MappingContext mappingContext)
        {
            return mappingContext.AccessibilityHelper.IsSymbolAccessible(fieldSymbol, via);
        }
    }
}