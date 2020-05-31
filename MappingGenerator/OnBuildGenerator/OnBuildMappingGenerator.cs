using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Mappings;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using SmartCodeGenerator.Sdk;

namespace MappingGenerator.OnBuildGenerator
{
    [CodeGenerator(typeof(MappingInterface))]
    public class OnBuildMappingGenerator : ICodeGenerator
    {
        private const string GeneratorName = "MappingGenerator.OnBuildMappingGenerator";
        private readonly MappingImplementorEngine ImplementorEngine = new MappingImplementorEngine();

        public Task<GenerationResult> GenerateAsync(CSharpSyntaxNode processedNode, AttributeData markerAttribute, TransformationContext context, CancellationToken cancellationToken)
        {
            var syntaxGenerator = SyntaxGenerator.GetGenerator(context.Document);
            var mappingDeclaration = (InterfaceDeclarationSyntax)processedNode;
            var interfaceSymbol = context.SemanticModel.GetDeclaredSymbol(mappingDeclaration);
            var mappingContext = new MappingContext(interfaceSymbol);
            var accessibilityHelper = new AccessibilityHelper(interfaceSymbol);
            foreach (var x in FindCustomMapperTypes(markerAttribute).SelectMany(CustomConversionHelper.FindCustomConversionMethods))
            {
                if (x.IsStatic && accessibilityHelper.IsSymbolAccessible(x, interfaceSymbol))
                {
                    mappingContext.CustomConversions[(x.Parameters[0].Type, x.ReturnType)] = (ExpressionSyntax)syntaxGenerator.MemberAccessExpression((ExpressionSyntax)syntaxGenerator.IdentifierName(x.ContainingType.ToDisplayString()), x.Name);
                }
            }
            
            var mappingClass = (ClassDeclarationSyntax)syntaxGenerator.ClassDeclaration(
                mappingDeclaration.Identifier.Text.Substring(1),
                accessibility: Accessibility.Public,
                modifiers: DeclarationModifiers.Partial,
                interfaceTypes: new List<SyntaxNode>()
                {
                    syntaxGenerator.TypeExpression(interfaceSymbol)
                },
                members: mappingDeclaration.Members.Select(x =>
                {
                    if (x is MethodDeclarationSyntax methodDeclaration)
                    {
                        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
                        var statements = ImplementorEngine.CanProvideMappingImplementationFor(methodSymbol) ? ImplementorEngine.GenerateMappingStatements(methodSymbol, syntaxGenerator, context.SemanticModel, mappingContext) :
                                new List<StatementSyntax>()
                                {
                                    GenerateThrowNotSupportedException(context, syntaxGenerator, methodSymbol.Name)
                                };

                        var methodDeclarationSyntax = ((MethodDeclarationSyntax)syntaxGenerator.MethodDeclaration(
                            methodDeclaration.Identifier.Text,
                            parameters: methodDeclaration.ParameterList.Parameters,
                            accessibility: Accessibility.Public,
                            modifiers: DeclarationModifiers.Virtual,
                            typeParameters: methodDeclaration.TypeParameterList?.Parameters.Select(xx => xx.Identifier.Text),
                            returnType: methodDeclaration.ReturnType
                        )).WithBody(SyntaxFactory.Block(statements));
                        return methodDeclarationSyntax;
                    }
                    return x;
                }));
            
            var newRoot = WrapInTheSameNamespace(mappingClass, processedNode);
            var usings = new List<UsingDirectiveSyntax>()
            {
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(interfaceSymbol.ContainingNamespace.ToDisplayString()))
            };

            var immutableList = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.IImmutableList`1");
            if (immutableList != null)
            {
                usings.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Immutable")));
            }

            return Task.FromResult(new GenerationResult()
            {
                Members = SyntaxFactory.SingletonList(newRoot),
                Usings = new SyntaxList<UsingDirectiveSyntax>(usings)
            });
        }

        private static IEnumerable<INamedTypeSymbol> FindCustomMapperTypes(AttributeData markerAttribute)
        {
            if (markerAttribute.NamedArguments != null)
            {
                foreach (var argument in markerAttribute.NamedArguments)
                {
                    if (argument.Key == nameof(MappingInterface.CustomStaticMappers) &&
                        argument.Value.Kind == TypedConstantKind.Array && argument.Value.Type is IArrayTypeSymbol arrayType &&
                        arrayType.ElementType.ToDisplayString() == "System.Type")
                    {
                        foreach (var typedConstant in argument.Value.Values)
                        {
                            if (typedConstant.Kind == TypedConstantKind.Type &&
                                typedConstant.Value is INamedTypeSymbol typeWithConverters)
                            {
                                yield return typeWithConverters;
                            }
                        }
                    }
                }
            }
        }

        private static StatementSyntax GenerateThrowNotSupportedException(TransformationContext context, SyntaxGenerator syntaxGenerator, string methodName)
        {
            var notImplementedExceptionType = context.SemanticModel.Compilation.GetTypeByMetadataName("System.NotSupportedException");
            var createNotImplementedException = syntaxGenerator.ObjectCreationExpression(notImplementedExceptionType,
                
                syntaxGenerator.LiteralExpression($"'{methodName}' method signature is not supported by {GeneratorName}"));
            return (StatementSyntax)syntaxGenerator.ThrowStatement(createNotImplementedException);
        }

        private static MemberDeclarationSyntax WrapInTheSameNamespace(ClassDeclarationSyntax generatedClass, SyntaxNode ancestor)
        {
            if (ancestor is NamespaceDeclarationSyntax namespaceDeclaration)
            {
                return SyntaxFactory.NamespaceDeclaration(namespaceDeclaration.Name.WithoutTrivia())
                    .AddMembers(generatedClass);
            }

            if (ancestor.Parent != null)
            {
                return WrapInTheSameNamespace(generatedClass, ancestor.Parent);
            }

            return generatedClass;
        }
    }
}