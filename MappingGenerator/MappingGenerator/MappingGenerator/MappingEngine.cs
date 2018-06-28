using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public class MappingEngine
    {
        private readonly SemanticModel semanticModel;
        private readonly SyntaxGenerator syntaxGenerator;

        private MappingEngine(SemanticModel semanticModel, SyntaxGenerator syntaxGenerator)
        {
            this.semanticModel = semanticModel;
            this.syntaxGenerator = syntaxGenerator;
        }

        public TypeInfo GetExpressionTypeInfo(SyntaxNode expression)
        {
            return semanticModel.GetTypeInfo(expression);
        }

        public static async Task<MappingEngine> Create(Document document, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            return new MappingEngine(semanticModel, syntaxGenerator);
        }

        public ExpressionSyntax MapExpression(ExpressionSyntax sourceExpression, ITypeSymbol sourceType, ITypeSymbol destinationType)
        {
            return new MappingElement(syntaxGenerator, semanticModel)
            {
                Expression = sourceExpression,
                ExpressionType  = sourceType
            }.AdjustToType(destinationType).Expression;
        }
    }
}