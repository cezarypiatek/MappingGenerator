using System.Collections.Generic;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using MappingGenerator.Mappings;
using MappingGenerator.RoslynHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using System.Linq;
using MappingGenerator.Mappings.MappingImplementors;

namespace MappingGenerator.Features.Refactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(ImplementCloneMethodRefactoring)), Shared]
    public class ImplementCloneMethodRefactoring : CodeRefactoringProvider
    {
        public const string Title = "Implement clone method";

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);
            if (node is ClassDeclarationSyntax || node is StructDeclarationSyntax)
            {
                var typeDeclarationSyntax = node as TypeDeclarationSyntax;
                if (typeDeclarationSyntax.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    return;
                }
                context.RegisterRefactoring(CodeAction.Create(title: Title, createChangedDocument: c => AddCloneImplementation(context.Document, typeDeclarationSyntax, c), equivalenceKey: Title));
            }

            if (node is MethodDeclarationSyntax md && IsCandidateForCloneMethod(md))
            {
                context.RegisterRefactoring(CodeAction.Create(title: Title, createChangedDocument: c => ImplementCloneMethodBody(context.Document, md, c), equivalenceKey: Title));
            }
        }

        private async Task<Document> ImplementCloneMethodBody(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
        {
            var generator = SyntaxGenerator.GetGenerator(document);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var methodSymbol =  semanticModel.GetDeclaredSymbol(methodDeclaration);
            var mappingContext = new MappingContext(methodSymbol.ContainingType);
            var cloneExpression = CreateCloneExpression(generator, semanticModel, new AnnotatedType(methodSymbol.ReturnType), mappingContext);
            return await document.ReplaceNodes(methodDeclaration.Body, ((BaseMethodDeclarationSyntax) generator.MethodDeclaration(methodSymbol, cloneExpression)).Body, cancellationToken);
        }

        private bool IsCandidateForCloneMethod(MethodDeclarationSyntax md)
        {
            return md.ParameterList.Parameters.Count == 0 &&
                   md.ReturnType.ToString() == FindContainingTypeDeclaration(md)?.Identifier.ToString();
        }

        private static TypeDeclarationSyntax FindContainingTypeDeclaration(SyntaxNode node)
        {
            return node.FindNearestContainer<ClassDeclarationSyntax, StructDeclarationSyntax>() as TypeDeclarationSyntax;
        }

        private async Task<Document> AddCloneImplementation(Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
        {
            var generator = SyntaxGenerator.GetGenerator(document);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            //TODO: If method exists, replace it
            var newClassDeclaration = typeDeclaration.AddMethods(new[]
            {
                GenerateCloneMethodStronglyTyped(generator, typeDeclaration, semanticModel),
                GenerateCloneMethod(generator)
            });

            if (newClassDeclaration.BaseList == null || newClassDeclaration.BaseList.Types.Any(x => x.Type.ToString().Contains("ICloneable")) == false)
            {
                var cloneableInterface = SyntaxFactory.ParseTypeName($"System.ICloneable");
                newClassDeclaration = generator.AddInterfaceType(newClassDeclaration, cloneableInterface.WithAdditionalAnnotations(Formatter.Annotation)) as TypeDeclarationSyntax;
            }
            
            return await document.ReplaceNodes(typeDeclaration, newClassDeclaration, cancellationToken);
        }

        private MethodDeclarationSyntax GenerateCloneMethodStronglyTyped(SyntaxGenerator generator,
            TypeDeclarationSyntax typeDeclaration, SemanticModel semanticModel)
        {
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
            var mappingContext = new MappingContext(typeDeclaration, semanticModel);
            var cloneExpression = CreateCloneExpression(generator, semanticModel, new AnnotatedType(typeSymbol), mappingContext);
            return generator.MethodDeclaration("Clone", 
                accessibility: Accessibility.Public,
                statements:cloneExpression,
                returnType: SyntaxFactory.ParseTypeName(typeDeclaration.Identifier.Text))
                .WithAdditionalAnnotations(Formatter.Annotation) as MethodDeclarationSyntax;
        }

        private MethodDeclarationSyntax GenerateCloneMethod(SyntaxGenerator generator)
        {
            var md = generator.MethodDeclaration("Clone", 
                statements:new []
                {
                    generator.ReturnStatement(generator.InvocationExpression(generator.IdentifierName("Clone")))
                },
                returnType: SyntaxFactory.ParseTypeName("object")) as MethodDeclarationSyntax;
            return md.WithExplicitInterfaceSpecifier(
                SyntaxFactory.ExplicitInterfaceSpecifier(SyntaxFactory.IdentifierName("ICloneable")));
            
        }

        private SyntaxNode[] CreateCloneExpression(SyntaxGenerator generator, SemanticModel semanticModel, AnnotatedType type, MappingContext mappingContext)
        {
            //TODO: If subtypes contains clone method use it, remember about casting
            var mappingEngine = new CloneMappingEngine(semanticModel, generator);
            var newExpression = mappingEngine.MapExpression((ExpressionSyntax)generator.ThisExpression(), type, type, mappingContext);
            return new[] { generator.ReturnStatement(newExpression).WithAdditionalAnnotations(Formatter.Annotation) };
        }
    }

    public static class TypeDeclarationExtensions
    {

        public static TypeDeclarationSyntax WithMembers(this TypeDeclarationSyntax td, IEnumerable<MemberDeclarationSyntax> newMembers)
        {
            switch (td)
            {
                case ClassDeclarationSyntax cd:
                    return cd.WithMembers(new SyntaxList<MemberDeclarationSyntax>(newMembers));
                case StructDeclarationSyntax sd:
                    return sd.WithMembers(new SyntaxList<MemberDeclarationSyntax>(newMembers));
                default:
                    return td;
            }
        }
    }
}
