using Microsoft.CodeAnalysis;

namespace MappingGenerator
{
    class WrapperInfo
    {
        public WrapperInfoType Type { get;}
        public IPropertySymbol UnwrappingProperty { get;  }
        public IMethodSymbol UnwrappingMethod { get;  }

        public WrapperInfo()
        {
            Type = WrapperInfoType.No;
        }

        public WrapperInfo(IPropertySymbol unwrappingProperty)
        {
            UnwrappingProperty = unwrappingProperty;
            Type = WrapperInfoType.Property;
        }

        public WrapperInfo(IMethodSymbol unwrappingMethod)
        {
            UnwrappingMethod = unwrappingMethod;
            Type = WrapperInfoType.Method;
        }
    }

    enum WrapperInfoType
    {
        No,
        Property,
        Method
    }
}