using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using RoslynNUnitLight;

namespace MappingGenerator.Test
{
    //TODO: Remove after fixing https://github.com/phoenix172/RoslynNUnitLight.NetStandard/issues/2
    public abstract class ExtendedCodeRefactoringTestFixture : CodeRefactoringTestFixture
    {
        protected void TestCodeRefactoring(string markupCode, string expected, int index)
        {
            Assert.That<bool>(TestHelpers.TryGetDocumentAndSpanFromMarkup(markupCode, this.LanguageName, this.References, out var document, out var span), (IResolveConstraint) Is.True);
            this.TestCodeRefactoring(document, span, expected, index);
        }

        protected void TestCodeRefactoring(Document document, TextSpan span, string expected, int index)
        {
            ImmutableArray<CodeAction> codeRefactorings = this.GetCodeRefactorings(document, span);
            Assert.That<int>(codeRefactorings.Length, Is.AtLeast(index+1));
            Verify.CodeAction(codeRefactorings[index], document, expected);
        }

        private ImmutableArray<CodeAction> GetCodeRefactorings(Document document, TextSpan span)
        {
            ImmutableArray<CodeAction>.Builder builder = ImmutableArray.CreateBuilder<CodeAction>();
            Action<CodeAction> registerRefactoring = (Action<CodeAction>)(a => builder.Add(a));
            this.CreateProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(document, span, registerRefactoring, CancellationToken.None)).Wait();
            return builder.ToImmutable();
        }
    }
}