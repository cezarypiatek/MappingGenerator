using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Features.Refactorings.Mapping.MappingImplementors;
using MappingGenerator.MethodHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator.Features.Refactorings.Mapping
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(MappingGeneratorRefactoring)), Shared]
    public class MappingGeneratorRefactoring : CodeRefactoringProvider
    {
        private const string title = "Generate mapping code";

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);

            await TryToRegisterRefactoring(context, node);
        }

        private async Task TryToRegisterRefactoring(CodeRefactoringContext context, SyntaxNode node)
        {
            switch (node)
            {
                case BaseMethodDeclarationSyntax methodDeclaration when IsMappingMethodCandidate(methodDeclaration):
                    if (methodDeclaration.Parent.Kind() != SyntaxKind.InterfaceDeclaration )
                    {
                        var semanticModel = await context.Document.GetSemanticModelAsync();
                        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);

                        if (IsCompleteMethodDeclarationSymbol(methodSymbol) == false)
                        {
                            return;
                        }

                        if (CanProvideMappingImplementationFor(methodSymbol))
                        {
                            var generateMappingAction = CodeAction.Create(title: title, createChangedDocument: async (c) => await GenerateMappingMethodBody(context.Document, methodDeclaration, c), equivalenceKey: title);
                            context.RegisterRefactoring(generateMappingAction);
                        }
                    }
                    break;
                case IdentifierNameSyntax _:
                case ParameterListSyntax _:
                   await TryToRegisterRefactoring(context, node.Parent);
                break;
            }
        }

        private static bool IsMappingMethodCandidate(BaseMethodDeclarationSyntax methodDeclaration)
        {
            if (methodDeclaration is MethodDeclarationSyntax)
            {
                return true;
            }

            if (methodDeclaration is ConstructorDeclarationSyntax constructorDeclaration && constructorDeclaration.ParameterList.Parameters.Count >= 1)
            {
                return true;
            }

            return false;
        }

        private static bool IsCompleteMethodDeclarationSymbol(IMethodSymbol methodSymbol)
        {
            var allParametersHaveNames = methodSymbol.Parameters.All(x => string.IsNullOrWhiteSpace(x.Name) == false);
            return allParametersHaveNames;
        }

        private bool CanProvideMappingImplementationFor(IMethodSymbol methodSymbol)
        {
            return this.implementors.Any(x => x.CanImplement(methodSymbol));
        }

        private async Task<Document> GenerateMappingMethodBody(Document document, BaseMethodDeclarationSyntax methodSyntax, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax);
            var generator = SyntaxGenerator.GetGenerator(document);
            var mappingExpressions = GenerateMappingCode(methodSymbol, generator, semanticModel);
            var blockSyntax = SyntaxFactory.Block(mappingExpressions.Select(e => e.AsStatement())).WithAdditionalAnnotations(Formatter.Annotation);
            return await document.ReplaceNodes(methodSyntax, methodSyntax.WithOnlyBody(blockSyntax), cancellationToken);
        }

        private  IEnumerable<SyntaxNode> GenerateMappingCode(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
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
    }
}
