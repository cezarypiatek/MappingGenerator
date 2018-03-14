using System.Collections.Generic;
using System.Composition;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.MethodHelpers;
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
                var invocation = new MethodInvocation(invocationExpression);
                context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with value parameters", createChangedDocument: c => GenerateSplatting(context.Document, invocation, false, c), equivalenceKey: "Generate splatting with value parameters"), diagnostic);
                context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with named parameters", createChangedDocument: c => GenerateSplatting(context.Document, invocation, true, c), equivalenceKey: "Generate splatting with named parameters"), diagnostic);
                return;
            }

            var creationExpression = token.Parent.FindContainer<ObjectCreationExpressionSyntax>();
            if (creationExpression != null && creationExpression.ArgumentList.Arguments.Count == 1)
            {
                var invocation = new ConstructorInvocation(creationExpression);
                context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with value parameters", createChangedDocument: c => GenerateSplatting(context.Document, invocation, false, c), equivalenceKey: "Generate splatting with value parameters"+"_constructor"), diagnostic);
                context.RegisterCodeFix(CodeAction.Create(title: "Generate splatting with named parameters", createChangedDocument: c => GenerateSplatting(context.Document, invocation, true, c), equivalenceKey: "Generate splatting with named parameters"+"_constructor"), diagnostic);
            }
        }

        private async Task<Document> GenerateSplatting(Document document, IInvocation invocation, bool generateNamedParameters, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var overloadParameterSets = invocation.GetOverloadParameterSets(semanticModel);
            if (overloadParameterSets != null)
            {
                var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
                var invalidArgumentList = invocation.Arguments;
                var parametersMatch = FindParametersMatch(invalidArgumentList, overloadParameterSets, semanticModel, syntaxGenerator);
                if (parametersMatch != null)
                {
                    var argumentList = parametersMatch.ToArgumentListSyntax(syntaxGenerator, generateNamedParameters);
                    return await document.ReplaceNodes(invocation.SourceNode, invocation.WithArgumentList(argumentList), cancellationToken);
                
                }
            }

            return document;
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
