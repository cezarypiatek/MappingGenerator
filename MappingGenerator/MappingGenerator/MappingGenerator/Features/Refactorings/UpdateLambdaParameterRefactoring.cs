using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Mappings;
using MappingGenerator.Mappings.MappingMatchers;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator.Features.Refactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(EmptyInitializationBlockRefactoring)), Shared]
    public class UpdateLambdaParameterRefactoring : CodeRefactoringProvider
    {
        private const string Title = "Update lambda parameter with local variables";

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);

            var container = node.FindContainer<LambdaExpressionSyntax>();
            if (container != null)
            {
                var semanticModel = await context.Document.GetSemanticModelAsync();
                var symbol = semanticModel.GetSymbolInfo(container).Symbol;
                if (symbol is IMethodSymbol methodSymbol && methodSymbol.Parameters.Length == 1 && methodSymbol.ReturnsVoid)
                {
                    context.RegisterRefactoring(CodeAction.Create(title: Title, createChangedDocument: c => InitializeWithLocals(context.Document, container, c), equivalenceKey: Title));
                }
            }
        }

        private async Task<Document> InitializeWithLocals(Document document, LambdaExpressionSyntax lambdaExpressionSyntax, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var sourceFinders = GetAllPossibleSourceFinders(lambdaExpressionSyntax, semanticModel, syntaxGenerator).ToList();
            var mappingMatcher = new BestPossibleMatcher(sourceFinders);
            return await ReplaceWithMappingBody(document, lambdaExpressionSyntax, semanticModel, mappingMatcher, cancellationToken);
        }

        private static IEnumerable<IMappingSourceFinder> GetAllPossibleSourceFinders(LambdaExpressionSyntax lambdaExpression, SemanticModel semanticModel, SyntaxGenerator syntaxGenerator)
        {
            var localSymbols = semanticModel.GetLocalSymbols(lambdaExpression);
            yield return new LocalScopeMappingSourceFinder(semanticModel, localSymbols);
            foreach (var localSymbol in localSymbols)
            {
                
                var symbolType = semanticModel.GetTypeForSymbol(localSymbol);
                
                if (symbolType !=null && ObjectHelper.IsSimpleType(symbolType) == false)
                {
                    yield return new ObjectMembersMappingSourceFinder(new AnnotatedType(symbolType), SyntaxFactory.IdentifierName(localSymbol.Name), syntaxGenerator);
                }
            }
        }

        private static async Task<Document> ReplaceWithMappingBody(Document document, LambdaExpressionSyntax lambda, SemanticModel semanticModel, IMappingMatcher mappingMatcher, CancellationToken cancellationToken)
        {
            var methodSymbol = (IMethodSymbol)semanticModel.GetSymbolInfo(lambda).Symbol;
            var createdObjectType = methodSymbol.Parameters.First().Type;
            var mappingEngine = await MappingEngine.Create(document, cancellationToken);
            var mappingContext = new MappingContext(lambda, semanticModel);
            var propertiesToSet = MappingTargetHelper.GetFieldsThaCanBeSetPublicly(createdObjectType, mappingContext);
            var statements = mappingEngine.MapUsingSimpleAssignment(propertiesToSet, mappingMatcher, mappingContext, globalTargetAccessor: SyntaxFactory.IdentifierName(GetParameterIdentifier(lambda)))
                .Select(x=>x.AsStatement().WithTrailingTrivia(SyntaxFactory.EndOfLine("\r\n")));
            
            var newLambda = UpdateLambdaBody(lambda, SyntaxFactory.Block(statements)).WithAdditionalAnnotations(Formatter.Annotation);
            return await document.ReplaceNodes(lambda, newLambda, cancellationToken);
        }

        private static SyntaxToken GetParameterIdentifier(LambdaExpressionSyntax lambda)
        {
            return lambda switch
            {
                SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier,
                ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.ParameterList.Parameters.FirstOrDefault().Identifier,
                _ => SyntaxFactory.Token(SyntaxKind.None)
            };
        }

        private static LambdaExpressionSyntax UpdateLambdaBody(LambdaExpressionSyntax lambda, BlockSyntax blockSyntax)
        {
            return lambda switch
            {
                SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.WithBody(blockSyntax),
                ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.WithBody(blockSyntax),
                _ => lambda
            };
        }

    }
}
