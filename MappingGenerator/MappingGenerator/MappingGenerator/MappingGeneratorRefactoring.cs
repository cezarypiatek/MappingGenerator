using System;
using System.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.MethodHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(MappingGeneratorRefactoring)), Shared]
    public class MappingGeneratorRefactoring : CodeRefactoringProvider
    {
        private const string title = "Generate mapping code";

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
                case MethodDeclarationSyntax methodDeclaration:
                    if (methodDeclaration.Parent.Kind() != SyntaxKind.InterfaceDeclaration && methodDeclaration.ParameterList.Parameters.Count > 0)
                    {
                        var semanticModel = await context.Document.GetSemanticModelAsync();
                        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
                        var allParameterHaveNames = methodSymbol.Parameters.All(x => string.IsNullOrWhiteSpace(x.Name) == false);
                        if (allParameterHaveNames == false)
                        {
                            return;
                        }

                        if (SymbolHelper.IsPureMappingFunction(methodSymbol) ||
                            SymbolHelper.IsMultiParameterPureFunction(methodSymbol) ||
                            SymbolHelper.IsUpdateThisObjectFunction(methodSymbol) ||
                            SymbolHelper.IsUpdateParameterFunction(methodSymbol) ||
                            SymbolHelper.IsMultiParameterUpdateThisObjectFunction(methodSymbol) 
                        )
                        {
                            context.RegisterRefactoring(CodeAction.Create(title: title, createChangedDocument: c => GenerateMappingMethodBody(context.Document, methodDeclaration, c), equivalenceKey: title));
                           
                        }
                    }
                    break;
                case ConstructorDeclarationSyntax constructorDeclaration:
                    if (constructorDeclaration.ParameterList.Parameters.Count >= 1)
                    {
                        context.RegisterRefactoring(CodeAction.Create(title: title, createChangedDocument: c => GenerateMappingMethodBody(context.Document, constructorDeclaration, c), equivalenceKey: title));
                    }
                    break;
                case IdentifierNameSyntax _:
                case ParameterListSyntax _:
                   await TryToRegisterRefactoring(context, node.Parent);
                break;
                    
            }
        }

        private async Task<Document> GenerateMappingMethodBody(Document document,
            BaseMethodDeclarationSyntax methodSyntax, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax);
            var generator = SyntaxGenerator.GetGenerator(document);
            var mappingExpressions = GenerateMappingCode(methodSymbol, generator, semanticModel);
            var blockSyntax = SyntaxFactory.Block(mappingExpressions.Select(AsStatement)).WithAdditionalAnnotations(Formatter.Annotation);
            return await document.ReplaceNodes(methodSyntax, methodSyntax.WithOnlyBody(blockSyntax), cancellationToken);
        }

        private StatementSyntax AsStatement(SyntaxNode node)
        {
            if (node is ExpressionSyntax expression)
                return SyntaxFactory.ExpressionStatement(expression);
            return (StatementSyntax)node;
        }

        private static IEnumerable<SyntaxNode> GenerateMappingCode(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {

            if (SymbolHelper.IsIdentityMappingFunction(methodSymbol))
            {
                var cloneMappingEngine = new CloneMappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
                var source = methodSymbol.Parameters[0];
                var targetType = methodSymbol.ReturnType;
                var newExpression = cloneMappingEngine.MapExpression((ExpressionSyntax)generator.IdentifierName(source.Name), source.Type, targetType);
                return new[] { generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation) };
            }

            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);

            if (SymbolHelper.IsPureMappingFunction(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var targetType = methodSymbol.ReturnType;
                var newExpression = mappingEngine.MapExpression((ExpressionSyntax)generator.IdentifierName(source.Name), source.Type, targetType);
                return new[] { generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation) };
            }
            
            if (SymbolHelper.IsMultiParameterPureFunction(methodSymbol))
            {
                var targetType = methodSymbol.ReturnType;
                var sourceFinder = new LocalScopeMappingSourceFinder(semanticModel, methodSymbol);
                var newExpression = mappingEngine.AddInitializerWithMapping((ObjectCreationExpressionSyntax)generator.ObjectCreationExpression(targetType), sourceFinder, targetType);
                return new[] { generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation) };
            }

            if (SymbolHelper.IsUpdateParameterFunction(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var target = methodSymbol.Parameters[1];
                var targets = ObjectHelper.GetFieldsThaCanBeSetPublicly(target.Type, methodSymbol.ContainingAssembly);
                var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator);
                return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder, globalTargetAccessor: generator.IdentifierName(target.Name));
            }

            if (SymbolHelper.IsUpdateThisObjectFunction(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator);
                var targets = ObjectHelper.GetFieldsThaCanBeSetPrivately(methodSymbol.ContainingType);
                return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder);
            }

            if (SymbolHelper.IsMultiParameterUpdateThisObjectFunction(methodSymbol))
            {
                var sourceFinder = new LocalScopeMappingSourceFinder(semanticModel, methodSymbol.Parameters);
                var targets = ObjectHelper.GetFieldsThaCanBeSetPrivately(methodSymbol.ContainingType);
                return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder);
            }

            if (SymbolHelper.IsMappingConstructor(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator);
                var targets = ObjectHelper.GetFieldsThaCanBeSetFromConstructor(methodSymbol.ContainingType);
                return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder);
            }

            if (SymbolHelper.IsMultiParameterMappingConstructor(methodSymbol))
            {
                var sourceFinder = new LocalScopeMappingSourceFinder(semanticModel, methodSymbol.Parameters);
                var targets = ObjectHelper.GetFieldsThaCanBeSetFromConstructor(methodSymbol.ContainingType);
                return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder);
            }
            return Enumerable.Empty<SyntaxNode>();
        }
    }
}
