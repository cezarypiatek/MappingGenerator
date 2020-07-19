using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Mappings;
using MappingGenerator.Mappings.MappingImplementors;
using MappingGenerator.Mappings.MappingMatchers;
using MappingGenerator.Mappings.SourceFinders;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator.Features.Refactorings
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
            if (node is InitializerExpressionSyntax objectInitializer && objectInitializer.Expressions.Count == 0)
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
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator= SyntaxGenerator.GetGenerator(document);
            var sourceFinders = GetAllPossibleSourceFinders(objectInitializer, semanticModel, syntaxGenerator).ToList();
            var mappingMatcher = new BestPossibleMatcher(sourceFinders);
            return await ReplaceEmptyInitializationBlock(document, objectInitializer, semanticModel, mappingMatcher, cancellationToken);
        }

        private static IEnumerable<IMappingSourceFinder> GetAllPossibleSourceFinders(InitializerExpressionSyntax objectInitializer, SemanticModel semanticModel, SyntaxGenerator syntaxGenerator)
        {
            var localSymbols = semanticModel.GetLocalSymbols(objectInitializer);

            var container = objectInitializer.FindNearestContainer<QueryExpressionSyntax, VariableDeclaratorSyntax>();

            switch (container)
            {
                case QueryExpressionSyntax queryExpression:
                    yield return GetMappingSourceFindersForQueryExpression(semanticModel, syntaxGenerator, queryExpression, localSymbols);
                    yield break;
                case VariableDeclaratorSyntax vds:
                {
                    var leftSymbol = semanticModel.GetDeclaredSymbol(vds);
                    localSymbols = localSymbols.Where(ls => ls != leftSymbol).ToList();
                    break;
                }
            }

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

        private static IMappingSourceFinder GetMappingSourceFindersForQueryExpression(SemanticModel semanticModel, SyntaxGenerator syntaxGenerator, QueryExpressionSyntax queryExpression, IReadOnlyList<ISymbol> localSymbols)
        {
            var queryLocation = queryExpression.GetLocation().SourceSpan;

            var queryVariablesSourceFinders = localSymbols.Where(s => s is IRangeVariableSymbol).Where(s =>
            {
                var symbolLocation = s.Locations.First().SourceSpan;
                return symbolLocation.Start >= queryLocation.Start && symbolLocation.End <= queryLocation.End;
            }).Select(s =>
            {
                var type = semanticModel.GetTypeForSymbol(s);
                if (ObjectHelper.IsSimpleType(type))
                {
                    return null;
                }

                if (type is INamedTypeSymbol namedType && type.ToDisplayString().StartsWith("System.Linq.IGrouping<"))
                {
                    var keyType = namedType.TypeArguments[0];
                    if (ObjectHelper.IsSimpleType(keyType) == false)
                    {
                        var accessor = syntaxGenerator.MemberAccessExpression(syntaxGenerator.IdentifierName(s.Name), syntaxGenerator.IdentifierName("Key"));
                        return (IMappingSourceFinder)new OrderedSourceFinder(new []
                        {
                            new ObjectMembersMappingSourceFinder(new AnnotatedType(type), syntaxGenerator.IdentifierName(s.Name), syntaxGenerator),
                            new ObjectMembersMappingSourceFinder(new AnnotatedType(keyType), accessor, syntaxGenerator)
                        });
                    }
                }

                return new ObjectMembersMappingSourceFinder(new AnnotatedType(type), syntaxGenerator.IdentifierName(s.Name), syntaxGenerator);
            }).Where(x => x != null).ToList();

            return new OrderedSourceFinder(queryVariablesSourceFinders);
        }

        private async Task<Document> InitializeWithLambdaParameter(Document document, LambdaExpressionSyntax lambdaSyntax, InitializerExpressionSyntax objectInitializer, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var generator = SyntaxGenerator.GetGenerator(document);

            var lambdaSymbol = semanticModel.GetSymbolInfo(lambdaSyntax).Symbol as IMethodSymbol;
            var firstArgument = lambdaSymbol.Parameters.First();
            var mappingSourceFinder = new ObjectMembersMappingSourceFinder(new AnnotatedType(firstArgument.Type), generator.IdentifierName(firstArgument.Name), generator);
            return await ReplaceEmptyInitializationBlock(document, objectInitializer, semanticModel, new SingleSourceMatcher(mappingSourceFinder), cancellationToken);
        }

        private async Task<Document> InitializeWithDefaults(Document document, InitializerExpressionSyntax objectInitializer, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var mappingSourceFinder = new ScaffoldingSourceFinder(syntaxGenerator, document);
            return await ReplaceEmptyInitializationBlock(document, objectInitializer, semanticModel, new SingleSourceMatcher(mappingSourceFinder), cancellationToken);
        }

        private static async Task<Document> ReplaceEmptyInitializationBlock(Document document,
            InitializerExpressionSyntax objectInitializer, SemanticModel semanticModel,
            IMappingMatcher mappingMatcher, CancellationToken cancellationToken)
        {
            var oldObjCreation = objectInitializer.FindContainer<ObjectCreationExpressionSyntax>();
            var createdObjectType = ModelExtensions.GetTypeInfo(semanticModel, oldObjCreation).Type;
            var mappingEngine = await MappingEngine.Create(document, cancellationToken);
            
            var newObjectCreation = mappingEngine.AddInitializerWithMapping(oldObjCreation, mappingMatcher, createdObjectType, new MappingContext(objectInitializer, semanticModel ));
            return await document.ReplaceNodes(oldObjCreation, newObjectCreation, cancellationToken);
        }
    }
}
