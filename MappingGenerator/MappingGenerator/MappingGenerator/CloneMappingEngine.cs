using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public class CloneMappingEngine: MappingEngine
    {
        public CloneMappingEngine(SemanticModel semanticModel, SyntaxGenerator syntaxGenerator, IAssemblySymbol contextAssembly) 
            : base(semanticModel, syntaxGenerator, contextAssembly)
        {
        }

        protected override bool ShouldCreateConversionBetweenTypes(ITypeSymbol targetType, ITypeSymbol sourceType)
        {
            if (targetType.Equals(sourceType) && SymbolHelper.IsNullable(targetType, out _))
            {
                return false;
            }

            return  ObjectHelper.IsSimpleType(targetType) == false && ObjectHelper.IsSimpleType(sourceType) == false;
        }

        protected override MappingElement TryToCreateMappingExpression(MappingElement source, ITypeSymbol targetType, MappingPath mappingPath)
        {
            //TODO: check if source is not null (conditional member access)

            if (mappingPath.Length > 1 && source.ExpressionType.AllInterfaces.Any(x => x.Name == "ICloneable") && source.ExpressionType.SpecialType!=SpecialType.System_Array)
            {

                var invokeClone = syntaxGenerator.InvocationExpression(syntaxGenerator.MemberAccessExpression(source.Expression, "Clone"));
                var cloneMethods = targetType.GetMembers("Clone");
                if (cloneMethods.Any(IsGenericCloneMethod))
                {
                    return new MappingElement()
                    {
                        ExpressionType = targetType,
                        Expression = invokeClone as ExpressionSyntax
                    };
                }

                var objectClone = cloneMethods.FirstOrDefault(x => x is IMethodSymbol md && md.Parameters.Length == 0);

                if (objectClone != null)
                {
                    var objectCLoneMethod = (IMethodSymbol) objectClone;

                    if(CanBeAccessedInCurrentContext(objectCLoneMethod) )
                    {
                        return new MappingElement()
                        {
                            ExpressionType = targetType,
                            Expression = syntaxGenerator.TryCastExpression(invokeClone, targetType) as ExpressionSyntax
                        };
                    }
                }

                var implicitClone = targetType.GetMembers("System.ICloneable.Clone").FirstOrDefault();
                if (implicitClone!=null)
                {
                    var castedOnICloneable = syntaxGenerator.CastExpression(SyntaxFactory.ParseTypeName("ICloneable"), source.Expression);
                    return new MappingElement()
                    {
                        ExpressionType = targetType,
                        Expression = syntaxGenerator.TryCastExpression(syntaxGenerator.InvocationExpression(syntaxGenerator.MemberAccessExpression(castedOnICloneable, "Clone")), targetType) as ExpressionSyntax
                    };

                }
            }

            return base.TryToCreateMappingExpression(source, targetType, mappingPath);
        }

        private bool IsGenericCloneMethod(ISymbol x)
        {
            return x is IMethodSymbol md &&
                   md.ReturnType.ToString().Equals("object", StringComparison.OrdinalIgnoreCase) == false &&
                   md.Parameters.Length == 0 &&
                   CanBeAccessedInCurrentContext(md);
        }

        private bool CanBeAccessedInCurrentContext(IMethodSymbol objectCLoneMethod)
        {
            return objectCLoneMethod.DeclaredAccessibility == Accessibility.Public || 
                   (objectCLoneMethod.DeclaredAccessibility == Accessibility.Internal && objectCLoneMethod.ContainingAssembly.IsSameAssemblyOrHasFriendAccessTo(contextAssembly));
        }
    }
}