using System;
using System.Reflection;
using Microsoft.CodeAnalysis;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;

namespace MappingGenerator.Mappings.SourceFinders
{
    public static class NullableExtensions
    {
        private static INullabilityResolver _nullabilityResolver;

        private static INullabilityResolver NullabilityResolver
        {

            get
            {
                return _nullabilityResolver ??= ReflectionNullabilityResolver.IsAvailable()
                    ? (INullabilityResolver) new ReflectionNullabilityResolver()
                    : new EmptyNullabilityResolver();
            }
        }

        public static bool CanBeNull(this IMethodSymbol methodSymbol) => methodSymbol.ReturnType.CanBeNull();
        public static AnnotatedType GetAnnotatedType(this TypeInfo typeInfo) => new AnnotatedType(typeInfo.Type);
        public static AnnotatedType GetAnnotatedTypeForConverted(this TypeInfo typeInfo) => new AnnotatedType(typeInfo.ConvertedType);
        public static bool CanBeNull(this ITypeSymbol typeSymbol) => NullabilityResolver.CanBeNull(typeSymbol);
        public static ITypeSymbol StripNullability(this ITypeSymbol type) => NullabilityResolver.StripNullability(type);
    }

    internal interface INullabilityResolver
    {
        bool CanBeNull(ITypeSymbol type);
        ITypeSymbol StripNullability(ITypeSymbol type);
    }

    class EmptyNullabilityResolver : INullabilityResolver
    {
        public bool CanBeNull(ITypeSymbol type) => false;

        public ITypeSymbol StripNullability(ITypeSymbol type) => type;
    }

    class ReflectionNullabilityResolver : INullabilityResolver
    {
        private readonly MethodInfo nullableAnnotation;
        private readonly MethodInfo withoutNullable;

        //NullableAnnotation.Annotated
        private readonly  object annotatedValue;
        //NullableAnnotation.None
        private readonly  object nonValue;
        private readonly  object[] stripValueParameter;

        private static readonly System.Reflection.TypeInfo typeSymbolInfo = typeof(ITypeSymbol).GetTypeInfo();

        public static bool IsAvailable() => typeSymbolInfo.GetDeclaredProperty("NullableAnnotation") != null;

        public ReflectionNullabilityResolver()
        {
            nullableAnnotation = typeSymbolInfo.GetDeclaredProperty("NullableAnnotation")!.GetMethod;
            withoutNullable = typeSymbolInfo.GetDeclaredMethod("WithNullableAnnotation");
            var nullableAnnotations = Enum.GetValues(withoutNullable.GetParameters()[0].ParameterType);
            annotatedValue = nullableAnnotations.GetValue(2);
            nonValue = nullableAnnotations.GetValue(0);
            stripValueParameter = new[] {nonValue};
        }


        public bool CanBeNull(ITypeSymbol type)
        {
            //var originalResult = type.NullableAnnotation == NullableAnnotation.Annotated;
            return nullableAnnotation.Invoke(type, Array.Empty<object>()).Equals(annotatedValue);
        }

        public ITypeSymbol StripNullability(ITypeSymbol type)
        {
           // return type.WithNullableAnnotation(NullableAnnotation.None);
           return (ITypeSymbol)withoutNullable.Invoke(type, stripValueParameter);
        }
    }
}