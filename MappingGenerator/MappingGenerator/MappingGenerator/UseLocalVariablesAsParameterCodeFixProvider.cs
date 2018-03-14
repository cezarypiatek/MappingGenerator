using System;
using System.Collections.Generic;
using System.Composition;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.MethodHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseLocalVariablesAsParameterCodeFixProvider)), Shared]
    public class UseLocalVariablesAsParameterCodeFixProvider : CodeFixProvider
    {
        private const string title = "Use local variables as parameters";
        private const string titleWithNamed = "Use local variables as named parameters";
        
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("CS1501", "CS7036");

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
            if (invocationExpression != null && invocationExpression.ArgumentList.Arguments.Count == 0)
            {
                var invocation = new MethodInvocation(invocationExpression);
                context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, invocation, false, c), equivalenceKey: title), diagnostic);
                context.RegisterCodeFix(CodeAction.Create(title: titleWithNamed, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, invocation, true, c), equivalenceKey: titleWithNamed), diagnostic);
                return;
            }

            var creationExpression = token.Parent.FindContainer<ObjectCreationExpressionSyntax>();
            if (creationExpression != null && creationExpression.ArgumentList.Arguments.Count == 0)
            {
                var invocation = new ConstructorInvocation(creationExpression);
                context.RegisterCodeFix(CodeAction.Create(title: title, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, invocation, false, c), equivalenceKey: title+"for constructor"), diagnostic);
                context.RegisterCodeFix(CodeAction.Create(title: titleWithNamed, createChangedDocument: c => UseLocalVariablesAsParameters(context.Document, invocation, true, c), equivalenceKey: titleWithNamed+"for constructor"), diagnostic);
            }
        }

        private async Task<Document> UseLocalVariablesAsParameters(Document document, IInvocation invocation, bool generateNamedParameters, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var mappingSourceFinder = new LocalScopeMappingSourceFinder(semanticModel, invocation.SourceNode);
            var overloadParameterSets = invocation.GetOverloadParameterSets(semanticModel);
            if (overloadParameterSets != null)
            {
                var parametersMatch = MethodHelper.FindBestParametersMatch(mappingSourceFinder, overloadParameterSets);
                if (parametersMatch != null)
                {
                    var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
                    var argumentList = parametersMatch.ToArgumentListSyntax(syntaxGenerator, generateNamedParameters);
                    return await  document.ReplaceNodes(invocation.SourceNode, invocation.WithArgumentList(argumentList), cancellationToken);
                }
            }
            return document;
        }
    }
}
