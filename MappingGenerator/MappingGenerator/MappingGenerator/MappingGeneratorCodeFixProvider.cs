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
            var mappingExpressions = GenerateMappingCode(methodSymbol, generator, semanticModel);
            return await document.ReplaceNodes(methodSyntax.Body, ((BaseMethodDeclarationSyntax) generator.MethodDeclaration(methodSymbol, mappingExpressions)).Body, cancellationToken);
        }

        private static IEnumerable<SyntaxNode> GenerateMappingCode(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            if (SymbolHelper.IsPureMappingFunction(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var targetType = methodSymbol.ReturnType;
                var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator, semanticModel);
                var objectCreationExpressionSyntax = MappingHelper.CreateObjectCreationExpressionWithInitializer(targetType, sourceFinder, generator, semanticModel);
                return new[] { generator.ReturnStatement(objectCreationExpressionSyntax) };
            }

            if (SymbolHelper.IsUpdateParameterFunction(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var target = methodSymbol.Parameters[1];
                var targets = ObjectHelper.GetFieldsThaCanBeSetPublicly(target.Type);
                var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator, semanticModel);
                return MapUsingSimpleAssigment(generator, semanticModel, targets, sourceFinder, generator.IdentifierName(target.Name));
            }

            if (SymbolHelper.IsUpdateThisObjectFunction(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator, semanticModel);
                var targets = ObjectHelper.GetFieldsThaCanBeSetPrivately(methodSymbol.ContainingType);
                return MapUsingSimpleAssigment(generator, semanticModel, targets, sourceFinder);
            }

            if (SymbolHelper.IsMultiParameterUpdateThisObjectFunction(methodSymbol))
            {
                var sourceFinder = new LocalScopeMappingSourceFinder(semanticModel, methodSymbol.Parameters, generator);
                var targets = ObjectHelper.GetFieldsThaCanBeSetPrivately(methodSymbol.ContainingType);
                return MapUsingSimpleAssigment(generator, semanticModel, targets, sourceFinder);
            }

            if (SymbolHelper.IsMappingConstructor(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator, semanticModel);
                var targets = ObjectHelper.GetFieldsThaCanBeSetFromConstructor(methodSymbol.ContainingType);
                return MapUsingSimpleAssigment(generator, semanticModel, targets, sourceFinder);
            }

            if (SymbolHelper.IsMultiParameterMappingConstructor(methodSymbol))
            {
                var sourceFinder = new LocalScopeMappingSourceFinder(semanticModel, methodSymbol.Parameters, generator);
                var targets = ObjectHelper.GetFieldsThaCanBeSetFromConstructor(methodSymbol.ContainingType);
                return MapUsingSimpleAssigment(generator, semanticModel, targets, sourceFinder);
            }
            return Enumerable.Empty<SyntaxNode>();
        }

        private static IEnumerable<SyntaxNode> MapUsingSimpleAssigment(SyntaxGenerator generator, SemanticModel semanticModel, IEnumerable<IPropertySymbol> targets, IMappingSourceFinder sourceFinder,  SyntaxNode gloablTargetAccessor = null)
        {
            return targets.Select(property => new
                {
                    source = sourceFinder.FindMappingSource(property.Name, property.Type),
                    target = new MappingElement(generator, semanticModel)
                    {
                        Expression = (ExpressionSyntax) CreateAccessPropertyExpression(gloablTargetAccessor, property, generator),
                        ExpressionType = property.Type
                    }
                })
                .Where(x=>x.source!=null)
                .Select(pair => generator.CompleteAssignmentStatement(pair.target.Expression, pair.source.Expression)).ToList();
        }

        private static SyntaxNode CreateAccessPropertyExpression(SyntaxNode gloablTargetAccessor,
            IPropertySymbol property, SyntaxGenerator generator)
        {
            if (gloablTargetAccessor == null)
            {
                return SyntaxFactory.IdentifierName(property.Name);
            }
            return generator.MemberAccessExpression(gloablTargetAccessor, property.Name);
        }
    }
}
