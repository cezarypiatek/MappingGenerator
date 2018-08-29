using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator
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
        }

        private async Task<Document> AddCloneImplementation(Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
        {
            //TODO:
            //public Foo Clone() { /* your code */ }
            //object ICloneable.Clone() { return Clone(); }

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
                newClassDeclaration = generator.AddBaseType(newClassDeclaration, cloneableInterface) as TypeDeclarationSyntax;
            }
            
            return await document.ReplaceNodes(typeDeclaration, newClassDeclaration, cancellationToken);
        }

        private MethodDeclarationSyntax GenerateCloneMethodStronglyTyped(SyntaxGenerator generator,
            TypeDeclarationSyntax typeDeclaration, SemanticModel semanticModel)
        {
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
            return generator.MethodDeclaration("Clone", 
                accessibility: Accessibility.Public,
               
                statements:CreateCloneExpression(generator, semanticModel, typeSymbol),
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

        private SyntaxNode[] CreateCloneExpression(SyntaxGenerator generator, SemanticModel semanticModel, INamedTypeSymbol type)
        {
            //TODO: If subtypes contains clone method use it, remember about casting
            var mappingEngine = new CloneMappingEngine(semanticModel, generator, type.ContainingAssembly);
            var newExpression = mappingEngine.MapExpression((ExpressionSyntax)generator.ThisExpression(), type, type);
            return new[] { generator.ReturnStatement(newExpression) };
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
