using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public class ScaffoldingSourceFinder:IMappingSourceFinder
    {
        private readonly SyntaxGenerator syntaxGenerator;

        public ScaffoldingSourceFinder(SyntaxGenerator syntaxGenerator)
        {
            this.syntaxGenerator = syntaxGenerator;
        }

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            return new MappingElement
            {
                ExpressionType = targetType,
                Expression = (ExpressionSyntax) GetDefaultExpression(targetType)
            };  
        }

        internal SyntaxNode GetDefaultExpression(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol namedTypeSymbol)
            {
                var enumOptions = namedTypeSymbol.MemberNames.ToList();
                if (enumOptions.Count > 0)
                {
                    return syntaxGenerator.MemberAccessExpression(syntaxGenerator.IdentifierName(namedTypeSymbol.Name), syntaxGenerator.IdentifierName(enumOptions[0]));
                }
                return syntaxGenerator.DefaultExpression(type);
            }

            if (type.SpecialType == SpecialType.None)
            {
                var objectCreationExpression = (ObjectCreationExpressionSyntax)syntaxGenerator.ObjectCreationExpression(type);

                if (MappingHelper.IsCollection(type))
                {
                    if (type is IArrayTypeSymbol)
                    {
                        objectCreationExpression = SyntaxFactory.ObjectCreationExpression((TypeSyntax)syntaxGenerator.TypeExpression(type));
                    }
                    else
                    {
                        //TODO: always create List<>
                    }



                    var subType = MappingHelper.GetElementType(type);
                    var subTypeDefault = (ExpressionSyntax)GetDefaultExpression(subType);
                    var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, new SeparatedSyntaxList<ExpressionSyntax>().Add(subTypeDefault)).FixInitializerExpressionFormatting(objectCreationExpression);
                    return objectCreationExpression
                        .WithInitializer(initializerExpressionSyntax);
                }

                {

                        var fields = ObjectHelper.GetFieldsThaCanBeSetPublicly(type);
                    var assignments = fields.Select(x =>
                    {
                        var identifier = (ExpressionSyntax)(SyntaxFactory.IdentifierName(x.Name));
                        return (ExpressionSyntax)syntaxGenerator.AssignmentStatement(identifier, this.FindMappingSource(x.Name, x.Type).Expression);
                    });
                    var initializerExpressionSyntax = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, new SeparatedSyntaxList<ExpressionSyntax>().AddRange(assignments)).FixInitializerExpressionFormatting(objectCreationExpression);
                    return objectCreationExpression.WithInitializer(initializerExpressionSyntax);
                }

                
            }


            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return syntaxGenerator.LiteralExpression(true);
                case SpecialType.System_SByte:
                    return syntaxGenerator.LiteralExpression(1);
                case SpecialType.System_Int16:
                    return  syntaxGenerator.LiteralExpression(16);
                case SpecialType.System_Int32:
                    return syntaxGenerator.LiteralExpression(32);
                case SpecialType.System_Int64:
                    return syntaxGenerator.LiteralExpression(64);
                case SpecialType.System_Byte:
                    return syntaxGenerator.LiteralExpression(1);
                case SpecialType.System_UInt16:
                    return syntaxGenerator.LiteralExpression(16u);
                case SpecialType.System_UInt32:
                    return syntaxGenerator.LiteralExpression(32u);
                case SpecialType.System_UInt64:
                    return syntaxGenerator.LiteralExpression(64u);
                case SpecialType.System_Single:
                    return syntaxGenerator.LiteralExpression(1.0f);
                case SpecialType.System_Double:
                    return syntaxGenerator.LiteralExpression(1.0);
                case SpecialType.System_Char:
                    return syntaxGenerator.LiteralExpression('a');
                case SpecialType.System_String:
                    return syntaxGenerator.LiteralExpression("lorem ipsum");
                case SpecialType.System_Decimal:
                    return syntaxGenerator.LiteralExpression(2.0m);
                case SpecialType.System_Object:
                    return  null;
                case SpecialType.System_Collections_IEnumerable: // 0x18
                case SpecialType.System_Collections_Generic_IEnumerable_T: // 0x19
                case SpecialType.System_Collections_Generic_IList_T:// 0x1A
                case SpecialType.System_Collections_Generic_ICollection_T:// 0x1B
                case SpecialType.System_Collections_IEnumerator: // 0x1C
                case SpecialType.System_Collections_Generic_IEnumerator_T:// 0x1D
                case SpecialType.System_Collections_Generic_IReadOnlyList_T:// 0x1E
                case SpecialType.System_Collections_Generic_IReadOnlyCollection_T: // 0x1F
                default:
                    return syntaxGenerator.LiteralExpression("ccc");
            }
        }

    }
}