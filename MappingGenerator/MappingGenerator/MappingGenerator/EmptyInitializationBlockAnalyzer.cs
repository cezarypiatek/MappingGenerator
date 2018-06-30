using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MappingGenerator
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EmptyInitializationBlockAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EmptyInitializationBlock";
        internal static readonly LocalizableString Title = "Initialization with local accessible variables can be generated";
        public static readonly LocalizableString MessageFormat = "Initialization with local accessible variables can be generated";
        internal const string Category = "Usage";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if(context == null) return;
            context.RegisterSyntaxNodeAction(AnalyzeObjectInitializationBlock, SyntaxKind.ObjectInitializerExpression);;
        }

        private void AnalyzeObjectInitializationBlock(SyntaxNodeAnalysisContext context)
        {
            var objectInitialization = context.Node as InitializerExpressionSyntax;
            if (objectInitialization != null && objectInitialization.Expressions.Count == 0)
            {
                var diagnostic = Diagnostic.Create(Rule, objectInitialization.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
