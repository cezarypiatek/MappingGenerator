using System.Collections.Generic;
using System.Composition;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SplattingCodeFixProvider)), Shared]
    public class SplattingCodeFixProvider : CodeFixProvider
    {
        private const string title = "Generate splatting";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("CS7036");

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            
            var invocationExpression = token.Parent.FindContainer<InvocationExpressionSyntax>();
            if (invocationExpression != null && invocationExpression.ArgumentList.Arguments.Count == 1)
            {
                context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with value parameters", createChangedDocument: c => GenerateSplatting(context.Document, invocationExpression, false, c), equivalenceKey: "Generate splatting with value parameters"), diagnostic);
                context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with named parameters", createChangedDocument: c => GenerateSplatting(context.Document, invocationExpression, true, c), equivalenceKey: "Generate splatting with named parameters"), diagnostic);
                return;
            }

            var creationExpression = token.Parent.FindContainer<ObjectCreationExpressionSyntax>();
            if (creationExpression != null && creationExpression.ArgumentList.Arguments.Count == 1)
            {
                context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with value parameters", createChangedDocument: c => GenerateSplatting(context.Document, creationExpression, false, c), equivalenceKey: "Generate splatting with value parameters"), diagnostic);
                context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with named parameters", createChangedDocument: c => GenerateSplatting(context.Document, creationExpression, true, c), equivalenceKey: "Generate splatting with named parameters"), diagnostic);
            }
            
        }

        private async Task<Document> GenerateSplatting(Document document,  InvocationExpressionSyntax invocationExpression, bool generateNamedParameters, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var methodSymbol = semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
            if (methodSymbol == null)
            {
                return document;
            }

            var invalidArgumentList = invocationExpression.ArgumentList;
            var overloadParameterSets = MethodHelper.GetOverloadParameterSets(methodSymbol, semanticModel);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var parametersMatch = FindParametersMatch(invalidArgumentList,overloadParameterSets, semanticModel, syntaxGenerator);
            if (parametersMatch == null)
            {
                return document;
            }

            var argumentList = parametersMatch.ToArgumentListSyntax(syntaxGenerator, generateNamedParameters);
            return await document.ReplaceNodes(invocationExpression, invocationExpression.WithArgumentList(argumentList), cancellationToken);
        }

        private async Task<Document> GenerateSplatting(Document document, ObjectCreationExpressionSyntax creationExpression, bool generateNamedParameters, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var invalidArgumentList = creationExpression.ArgumentList;
            var instantiatedType = (INamedTypeSymbol)semanticModel.GetSymbolInfo(creationExpression.Type).Symbol;
            var overloadParameterSets = instantiatedType.Constructors.Select(x => x.Parameters);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var parametersMatch = FindParametersMatch(invalidArgumentList, overloadParameterSets, semanticModel, syntaxGenerator);
            if (parametersMatch == null)
            {
                return document;
            }

            var argumentList = parametersMatch.ToArgumentListSyntax(syntaxGenerator, generateNamedParameters);
            return await document.ReplaceNodes(creationExpression, creationExpression.WithArgumentList(argumentList), cancellationToken);
        }

        private static MatchedParameterList FindParametersMatch(ArgumentListSyntax invalidArgumentList, IEnumerable<ImmutableArray<IParameterSymbol>> overloadParameterSets, SemanticModel semanticModel, SyntaxGenerator syntaxGenerator) 
        {
            var sourceFinder = CreateSourceFinderBasedOnInvalidArgument(invalidArgumentList, semanticModel, syntaxGenerator);
            var parametersMatch = MethodHelper.FindBestParametersMatch(sourceFinder, overloadParameterSets);
            return parametersMatch;
        }

        private static ObjectMembersMappingSourceFinder CreateSourceFinderBasedOnInvalidArgument(ArgumentListSyntax invalidArgumentList, SemanticModel semanticModel, SyntaxGenerator syntaxGenerator)
        {
            var invalidArgument = invalidArgumentList.Arguments.First();
            var sourceType = semanticModel.GetTypeInfo(invalidArgument.Expression).Type;
            return new ObjectMembersMappingSourceFinder(sourceType, invalidArgument.Expression, syntaxGenerator, semanticModel);
        }
    }
}
