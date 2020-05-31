using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;

namespace MappingGenerator.Mappings
{
    class WrapperInfo
    {
        public WrapperInfoType Type { get;}
        public IObjectField UnwrappingObjectField { get;  }
        public IMethodSymbol UnwrappingMethod { get;  }

        public WrapperInfo()
        {
            Type = WrapperInfoType.No;
        }

        public WrapperInfo(IObjectField unwrappingObjectField)
        {
            UnwrappingObjectField = unwrappingObjectField;
            Type = WrapperInfoType.ObjectField;
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
        ObjectField,
        Method
    }
}