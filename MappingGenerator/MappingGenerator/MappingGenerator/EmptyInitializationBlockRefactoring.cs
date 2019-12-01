using System;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(EmptyInitializationBlockRefactoring)), Shared]
    public class EmptyInitializationBlockRefactoring : CodeRefactoringProvider
    {
        private const string TitleForLocal = "Initialize with local variables";
        private const string TitleForLambda = "Initialize with lambda parameter";
        private const string TitleForScaffolding = "Initialize with sample values";

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);
            var objectInitializer = node as InitializerExpressionSyntax;
            if (objectInitializer != null && objectInitializer.Expressions.Count == 0)
            {
                context.RegisterRefactoring(CodeAction.Create(title: TitleForLocal, createChangedDocument: c => InitializeWithLocals(context.Document, objectInitializer, c), equivalenceKey: TitleForLocal));
                context.RegisterRefactoring(CodeAction.Create(title: TitleForScaffolding, createChangedDocument: c => InitializeWithDefaults(context.Document, objectInitializer, c), equivalenceKey: TitleForScaffolding));

                var lambda = objectInitializer.Parent.FindContainer<LambdaExpressionSyntax>();

                switch (lambda)
                {
                    case ParenthesizedLambdaExpressionSyntax parenthesizedLambda when parenthesizedLambda.ParameterList.Parameters.Count == 1:
                    case SimpleLambdaExpressionSyntax _:
                        context.RegisterRefactoring(CodeAction.Create(title: TitleForLambda, createChangedDocument: c => InitializeWithLambdaParameter(context.Document, lambda, objectInitializer, c), equivalenceKey: TitleForLambda));
                        break;
                }
            }
        }

        private async Task<Document> InitializeWithLocals(Document document, InitializerExpressionSyntax objectInitializer, CancellationToken cancellationToken)
        {
            var generator = SyntaxGenerator.GetGenerator(document);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var mappingSourceFinder = new LocalScopeMappingSourceFinder(semanticModel, objectInitializer);
            return await ReplaceEmptyInitializationBlock(document, objectInitializer, semanticModel, mappingSourceFinder, cancellationToken);
        }

        private async Task<Document> InitializeWithLambdaParameter(Document document, LambdaExpressionSyntax lambdaSyntax, InitializerExpressionSyntax objectInitializer, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var generator = SyntaxGenerator.GetGenerator(document);

            var lambdaSymbol = semanticModel.GetSymbolInfo(lambdaSyntax).Symbol as IMethodSymbol;
            var firstArgument = lambdaSymbol.Parameters.First();
            var mappingSourceFinder = new ObjectMembersMappingSourceFinder(firstArgument.Type, generator.IdentifierName(firstArgument.Name), generator);
            return await ReplaceEmptyInitializationBlock(document, objectInitializer, semanticModel, mappingSourceFinder, cancellationToken);
        }

        private async Task<Document> InitializeWithDefaults(Document document, InitializerExpressionSyntax objectInitializer, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var mappingSourceFinder = new ScaffoldingSourceFinder(syntaxGenerator, document, semanticModel.FindContextAssembly(objectInitializer));
            return await ReplaceEmptyInitializationBlock(document, objectInitializer, semanticModel, mappingSourceFinder, cancellationToken);
        }

        private static async Task<Document> ReplaceEmptyInitializationBlock(Document document,
            InitializerExpressionSyntax objectInitializer, SemanticModel semanticModel,
            IMappingSourceFinder mappingSourceFinder, CancellationToken cancellationToken)
        {
            var oldObjCreation = objectInitializer.FindContainer<ObjectCreationExpressionSyntax>();
            var createdObjectType = ModelExtensions.GetTypeInfo(semanticModel, oldObjCreation).Type;
            var mappingEngine = await MappingEngine.Create(document, cancellationToken, semanticModel.FindContextAssembly(objectInitializer));
            var newObjectCreation = mappingEngine.AddInitializerWithMapping(oldObjCreation, mappingSourceFinder, createdObjectType);
            return await document.ReplaceNodes(oldObjCreation, newObjectCreation, cancellationToken);
        }
    }
}
