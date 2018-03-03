using System;
using System.Composition;
using System.Collections.Generic;
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
            
            var statement = FindMethodInvocation(token.Parent);
            if (statement == null)
            {
                return;
            }
            context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => GenerateSplatting(context.Document, statement, c), equivalenceKey: title), diagnostic);
        }

        private async Task<Document> GenerateSplatting(Document document, InvocationExpressionSyntax invocationExpression, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var methodSymbol = semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
            if (methodSymbol == null)
            {
                return document;
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var invalidArgument = invocationExpression.ArgumentList.Arguments.First();
            var sourceType = semanticModel.GetTypeInfo(invalidArgument.Expression);
            var sourceMembers = ObjectHelper.GetPublicPropertySymbols(sourceType.Type).ToList();
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var argumentList = FindBestArgumentsMatch(methodSymbol, sourceMembers, syntaxGenerator, invalidArgument, cancellationToken);
            if (argumentList == null)
            {
                return document;
            }
            var newRoot = root.ReplaceNode(invocationExpression, invocationExpression.WithArgumentList(argumentList));
            return document.WithSyntaxRoot(newRoot);
        }

        private static ArgumentListSyntax FindBestArgumentsMatch(IMethodSymbol methodSymbol, IReadOnlyList<IPropertySymbol> sourceMembers, SyntaxGenerator syntaxGenerator, ArgumentSyntax invalidArgument, CancellationToken cancellationToken)
        {
            return methodSymbol.DeclaringSyntaxReferences.Select(ds =>
                {
                    var declaration = (MethodDeclarationSyntax) ds.GetSyntax(cancellationToken);
                    return FindArgumentsMatch(sourceMembers, syntaxGenerator, invalidArgument, declaration);
                })
                .Where(x => x.Arguments.Count > 0)
                .OrderByDescending(argumentList => argumentList.Arguments.Count)
                .FirstOrDefault();
        }

        private static ArgumentListSyntax FindArgumentsMatch(IReadOnlyList<IPropertySymbol> sourceMembers, SyntaxGenerator syntaxGenerator, ArgumentSyntax invalidArgument, MethodDeclarationSyntax declaration)
        {
            var argumentList = SyntaxFactory.ArgumentList();
            foreach (var parameter in declaration.ParameterList.Parameters)
            {
                var sourceMember = sourceMembers.FirstOrDefault(x => x.Name.ToLower() == parameter.Identifier.Text.ToLower());
                if (sourceMember != null)
                {
                    var sourceAccess = syntaxGenerator.MemberAccessExpression(invalidArgument.Expression, sourceMember.Name);
                    var argument = SyntaxFactory.Argument(SyntaxFactory.NameColon(parameter.Identifier.Text), SyntaxFactory.Token(SyntaxKind.None), (ExpressionSyntax) sourceAccess);
                    argumentList = argumentList.AddArguments(argument);
                }
            }
            return argumentList;
        }

        private InvocationExpressionSyntax FindMethodInvocation(SyntaxNode tokenParent)
        {
            if (tokenParent is InvocationExpressionSyntax invocation)
            {
                return invocation;
            }

            return tokenParent.Parent == null ? null : FindMethodInvocation(tokenParent.Parent);
        }
    }
}
