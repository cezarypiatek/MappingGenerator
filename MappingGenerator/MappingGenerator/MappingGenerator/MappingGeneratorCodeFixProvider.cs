using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MappingGeneratorCodeFixProvider)), Shared]
    public class MappingGeneratorCodeFixProvider : CodeFixProvider
    {
        private const string title = "Generate mapping code";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(MappingGeneratorAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<BaseMethodDeclarationSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => GenerateMappingMethodBody(context.Document, declaration, c), 
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Document> GenerateMappingMethodBody(Document document, BaseMethodDeclarationSyntax methodSyntax, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax);
            var generator = SyntaxGenerator.GetGenerator(document);
            var mappingGenerator = new MappingGenerator(generator, semanticModel);
            var mappingExpressions = GenerateMappingCode(methodSymbol, generator, mappingGenerator, semanticModel);
            return await document.ReplaceNodes(methodSyntax.Body, ((BaseMethodDeclarationSyntax) generator.MethodDeclaration(methodSymbol, mappingExpressions)).Body, cancellationToken);
        }

        private static IEnumerable<SyntaxNode> GenerateMappingCode(IMethodSymbol methodSymbol,
            SyntaxGenerator generator, MappingGenerator mappingGenerator, SemanticModel semanticModel)
        {
            if (SymbolHelper.IsPureMappingFunction(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var targetType = methodSymbol.ReturnType;
                return mappingGenerator.MapTypes(source.Type, targetType,generator.IdentifierName(source.Name));
            }

            if (SymbolHelper.IsUpdateThisObjectFunction(methodSymbol) || SymbolHelper.IsMappingConstructor(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var targetType = methodSymbol.ContainingType;
                return mappingGenerator.MapTypes(source.Type, targetType, generator.IdentifierName(source.Name), generator.ThisExpression(), targetExists: true);
            }

            if (SymbolHelper.IsUpdateParameterFunction(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var target = methodSymbol.Parameters[1];
                return mappingGenerator.MapTypes(source.Type, target.Type, generator.IdentifierName(source.Name), generator.IdentifierName(target.Name), targetExists: true);
            }

            if (SymbolHelper.IsMultiParameterUpdateThisObjectFunction(methodSymbol) || SymbolHelper.IsMultiParameterMappingConstructor(methodSymbol))
            {
                var sourceFinder = new LocalScopeMappingSourceFinder(semanticModel, methodSymbol.Parameters);
                return ObjectHelper.GetPublicPropertySymbols(methodSymbol.ContainingType)
                .Where(property => property.SetMethod!=null)
                .Select(property => new
                {
                    source = sourceFinder.FindMappingSource(property.Name, property.Type),
                    target = property
                })
                .SelectMany(pair => mappingGenerator.Map(pair.target, pair.source, generator.IdentifierName(pair.target.Name)));
            }
            return Enumerable.Empty<SyntaxNode>();
        }
    }
}
