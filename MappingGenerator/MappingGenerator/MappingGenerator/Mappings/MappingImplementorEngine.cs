using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        private async Task<IReadOnlyList<SyntaxNode>> GenerateMappingCode(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel, MappingContext mappingContext)
        {
            var matchedImplementor = implementors.FirstOrDefault(x => x.CanImplement(methodSymbol));
            if (matchedImplementor != null)
            {
                return await matchedImplementor.GenerateImplementation(methodSymbol, generator, semanticModel, mappingContext).ConfigureAwait(false);
            }
            return Array.Empty<SyntaxNode>();
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

        public async Task<BlockSyntax> GenerateMappingBlockAsync(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel, MappingContext mappingContext)
        {
            var mappingStatements = await  GenerateMappingStatements(methodSymbol, generator, semanticModel, mappingContext).ConfigureAwait(false);
            return SyntaxFactory.Block(mappingStatements).WithAdditionalAnnotations(Formatter.Annotation);
        }

        public async Task<IReadOnlyList<StatementSyntax>> GenerateMappingStatements(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel, MappingContext mappingContext)
        {
            var mappingExpressions = await GenerateMappingCode(methodSymbol, generator, semanticModel, mappingContext).ConfigureAwait(false);
            var mappingStatements = mappingExpressions.Select(e => e.AsStatement());
            return mappingStatements.ToList();
        }
    }
}