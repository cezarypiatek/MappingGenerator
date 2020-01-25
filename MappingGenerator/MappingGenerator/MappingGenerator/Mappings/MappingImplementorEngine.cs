using System.Collections.Generic;
using System.Linq;
using MappingGenerator.Mappings.MappingImplementors;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator.Mappings
{
    public class MappingImplementorEngine
    {
        private static bool IsCompleteMethodDeclarationSymbol(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.All(x => string.IsNullOrWhiteSpace(x.Name) == false);
        }

        public bool CanProvideMappingImplementationFor(IMethodSymbol methodSymbol)
        {

            if (IsCompleteMethodDeclarationSymbol(methodSymbol) == false)
            {
                return false;
            }

            return this.implementors.Any(x => x.CanImplement(methodSymbol));
        }

        public IEnumerable<SyntaxNode> GenerateMappingCode(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var matchedImplementor = implementors.FirstOrDefault(x => x.CanImplement(methodSymbol));
            if (matchedImplementor != null)
            {
                return matchedImplementor.GenerateImplementation(methodSymbol, generator, semanticModel);
            }
            return Enumerable.Empty<SyntaxNode>();
        }

        private readonly IReadOnlyList<IMappingMethodImplementor> implementors = new List<IMappingMethodImplementor>()
        {
            new IdentityMappingMethodImplementor(),
            new SingleParameterPureMappingMethodImplementor(),
            new MultiParameterPureMappingMethodImplementor(),
            new FallbackMappingImplementor(new UpdateSecondParameterMappingMethodImplementor(),new UpdateThisObjectMultiParameterMappingMethodImplementor()),
            new FallbackMappingImplementor(new UpdateThisObjectSingleParameterMappingMethodImplementor(),  new UpdateThisObjectMultiParameterMappingMethodImplementor()),
            new UpdateThisObjectMultiParameterMappingMethodImplementor(),
            new FallbackMappingImplementor(new SingleParameterMappingConstructorImplementor(),new MultiParameterMappingConstructorImplementor()),
            new MultiParameterMappingConstructorImplementor(),
            new ThisObjectToOtherMappingMethodImplementor()
        };

        public BlockSyntax GenerateMappingBlock(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingStatements = GenerateMappingStatements(methodSymbol, generator, semanticModel);
            return SyntaxFactory.Block(mappingStatements).WithAdditionalAnnotations(Formatter.Annotation);
        }

        public IEnumerable<StatementSyntax> GenerateMappingStatements(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingExpressions = GenerateMappingCode(methodSymbol, generator, semanticModel);
            var mappingStatements = mappingExpressions.Select(e => e.AsStatement());
            return mappingStatements;
        }
    }
}