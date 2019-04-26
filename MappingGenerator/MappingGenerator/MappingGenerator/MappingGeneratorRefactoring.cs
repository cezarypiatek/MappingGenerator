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
                    if (methodDeclaration.Parent.Kind() != SyntaxKind.InterfaceDeclaration )
                    {
                        var semanticModel = await context.Document.GetSemanticModelAsync();
                        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);

                        if (IsCompleteMethodDeclarationSymbol(methodSymbol) == false)
                        {
                            return;
                        }

                        if (IsMappingMethod(methodSymbol))
                        {
                            context.RegisterRefactoring(CodeAction.Create(title: title, createChangedDocument: c => GenerateMappingMethodBody(context.Document, methodDeclaration, c), equivalenceKey: title));
                           
                        }
                    }
                    break;
                case ConstructorDeclarationSyntax constructorDeclaration:
                    if (IsMappingConstructor(constructorDeclaration))
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

        private static bool IsCompleteMethodDeclarationSymbol(IMethodSymbol methodSymbol)
        {
            var allParameterHaveNames = methodSymbol.Parameters.All(x => string.IsNullOrWhiteSpace(x.Name) == false);
            return allParameterHaveNames;
        }

        private static bool IsMappingConstructor(ConstructorDeclarationSyntax constructorDeclaration)
        {
            return constructorDeclaration.ParameterList.Parameters.Count >= 1;
        }

        private static bool IsMappingMethod(IMethodSymbol methodSymbol)
        {
            return SymbolHelper.IsPureMappingFunction(methodSymbol) ||
                   SymbolHelper.IsMultiParameterPureFunction(methodSymbol) ||
                   SymbolHelper.IsUpdateThisObjectFunction(methodSymbol) ||
                   SymbolHelper.IsUpdateParameterFunction(methodSymbol) ||
                   SymbolHelper.IsMultiParameterUpdateThisObjectFunction(methodSymbol) ||
                   SymbolHelper.IsThisObjectToOtherConvert(methodSymbol);
        }

        private async Task<Document> GenerateMappingMethodBody(Document document, BaseMethodDeclarationSyntax methodSyntax, CancellationToken cancellationToken)
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
                return GenerateIdentityMappingFunctionExpressions(methodSymbol, generator, semanticModel);
            }
            
            if (SymbolHelper.IsPureMappingFunction(methodSymbol))
            {
                return GenerateSingleParameterPureMappingFunctionExpressions(methodSymbol, generator, semanticModel);
            }
            
            if (SymbolHelper.IsMultiParameterPureFunction(methodSymbol))
            {
                return GenerateMultiParameterPureMappingFunctionExpressions(methodSymbol, generator, semanticModel);
            }

            if (SymbolHelper.IsUpdateParameterFunction(methodSymbol))
            {
                return GenerateUpdateSecondParameterFunctionExpressions(methodSymbol, generator, semanticModel);
            }

            if (SymbolHelper.IsUpdateThisObjectFunction(methodSymbol))
            {
                return GenerateSingleParameterUpdateThisObjectFunctionExpressions(methodSymbol, generator, semanticModel);
            }

            if (SymbolHelper.IsMultiParameterUpdateThisObjectFunction(methodSymbol))
            {
                return GenerateMultiParameterUpdateThisObjectFunctionExpressions(methodSymbol, generator, semanticModel);
            }

            if (SymbolHelper.IsMappingConstructor(methodSymbol))
            {
                return GenerateSingleParameterMappingConstructor(methodSymbol, generator, semanticModel);
            }

            if (SymbolHelper.IsMultiParameterMappingConstructor(methodSymbol))
            {
                return GenerateMultiParameterMappingConstructor(methodSymbol, generator, semanticModel);
            }

            if (SymbolHelper.IsThisObjectToOtherConvert(methodSymbol))
            {
                return GenerateThisObjectToOtherFunctionExpressions(methodSymbol, generator, semanticModel);
            }

            return Enumerable.Empty<SyntaxNode>();
        }

        private static IEnumerable<SyntaxNode> GenerateThisObjectToOtherFunctionExpressions(IMethodSymbol methodSymbol,
            SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var targetType = methodSymbol.ReturnType;
            var newExpression = mappingEngine.MapExpression((ExpressionSyntax) generator.ThisExpression(), methodSymbol.ContainingType, targetType);
            return new[] {generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation)};
        }

        private static IEnumerable<SyntaxNode> GenerateMultiParameterMappingConstructor(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var sourceFinder = new LocalScopeMappingSourceFinder(semanticModel, methodSymbol.Parameters);
            var targets = ObjectHelper.GetFieldsThaCanBeSetFromConstructor(methodSymbol.ContainingType);
            return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder);
        }

        private static IEnumerable<SyntaxNode> GenerateSingleParameterMappingConstructor(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var source = methodSymbol.Parameters[0];
            var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator);
            var targets = ObjectHelper.GetFieldsThaCanBeSetFromConstructor(methodSymbol.ContainingType);
            return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder);
        }

        private static IEnumerable<SyntaxNode> GenerateMultiParameterUpdateThisObjectFunctionExpressions(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var sourceFinder = new LocalScopeMappingSourceFinder(semanticModel, methodSymbol.Parameters);
            var targets = ObjectHelper.GetFieldsThaCanBeSetPrivately(methodSymbol.ContainingType);
            return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder);
        }

        private static IEnumerable<SyntaxNode> GenerateSingleParameterUpdateThisObjectFunctionExpressions(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var source = methodSymbol.Parameters[0];
            var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator);
            var targets = ObjectHelper.GetFieldsThaCanBeSetPrivately(methodSymbol.ContainingType);
            return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder);
        }

        private static IEnumerable<SyntaxNode> GenerateUpdateSecondParameterFunctionExpressions(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var source = methodSymbol.Parameters[0];
            var target = methodSymbol.Parameters[1];
            var targets = ObjectHelper.GetFieldsThaCanBeSetPublicly(target.Type, methodSymbol.ContainingAssembly);
            var sourceFinder = new ObjectMembersMappingSourceFinder(source.Type, generator.IdentifierName(source.Name), generator);
            return mappingEngine.MapUsingSimpleAssignment(generator, targets, sourceFinder, globalTargetAccessor: generator.IdentifierName(target.Name));
        }

        private static IEnumerable<SyntaxNode> GenerateMultiParameterPureMappingFunctionExpressions(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var targetType = methodSymbol.ReturnType;
            var sourceFinder = new LocalScopeMappingSourceFinder(semanticModel, methodSymbol);
            var newExpression = mappingEngine.AddInitializerWithMapping(
                (ObjectCreationExpressionSyntax) generator.ObjectCreationExpression(targetType), sourceFinder, targetType);
            return new[] {generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation)};
        }

        private static IEnumerable<SyntaxNode> GenerateSingleParameterPureMappingFunctionExpressions(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var mappingEngine = new MappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var source = methodSymbol.Parameters[0];
            var targetType = methodSymbol.ReturnType;
            var newExpression = mappingEngine.MapExpression((ExpressionSyntax) generator.IdentifierName(source.Name),
                source.Type, targetType);
            return new[] {generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation)};
        }

        private static IEnumerable<SyntaxNode> GenerateIdentityMappingFunctionExpressions(IMethodSymbol methodSymbol, SyntaxGenerator generator, SemanticModel semanticModel)
        {
            var cloneMappingEngine = new CloneMappingEngine(semanticModel, generator, methodSymbol.ContainingAssembly);
            var source = methodSymbol.Parameters[0];
            var targetType = methodSymbol.ReturnType;
            var newExpression = cloneMappingEngine.MapExpression((ExpressionSyntax) generator.IdentifierName(source.Name),
                source.Type, targetType);
            return new[] {generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation)};
        }
    }
}
