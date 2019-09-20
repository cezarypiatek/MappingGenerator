using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.MethodHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Features.Refactorings.Mapping
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(MappingGeneratorRefactoring)), Shared]
    public class MappingGeneratorRefactoring : CodeRefactoringProvider
    {
        private const string title = "Generate mapping code";

        private static readonly MappingImplementorEngine MappingImplementorEngine = new MappingImplementorEngine();

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


                        if (MappingImplementorEngine.CanProvideMappingImplementationFor(methodSymbol))
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

        private async Task<Document> GenerateMappingMethodBody(Document document, BaseMethodDeclarationSyntax methodSyntax, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax);
            var generator = SyntaxGenerator.GetGenerator(document);
            var blockSyntax = MappingImplementorEngine.GenerateMappingBlock(methodSymbol, generator, semanticModel);
            return await document.ReplaceNodes(methodSyntax, methodSyntax.WithOnlyBody(blockSyntax), cancellationToken);
        }
    }
}
