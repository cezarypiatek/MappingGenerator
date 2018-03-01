using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MappingGenerator
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MappingGeneratorAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MappingGenerator";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static DiagnosticDescriptor MappingMethodRule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(MappingMethodRule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            if (context == null) return;

            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        }

        private void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
        {
            var methodNode = context.Node as ConstructorDeclarationSyntax;
            if (methodNode != null && methodNode.ParameterList.Parameters.Count ==1)
            {
                var diagnostic = Diagnostic.Create(MappingMethodRule, methodNode.Identifier.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var methodNode = context.Node as MethodDeclarationSyntax;
            if (methodNode != null && methodNode.ParameterList.Parameters.Count > 0 && methodNode.ParameterList.Parameters.Count < 3)
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodNode);
                var allParameterHaveNames = methodSymbol.Parameters.All(x => string.IsNullOrWhiteSpace(x.Name) == false);
                if (allParameterHaveNames == false)
                {
                    return;
                }

                if (SymbolHelper.IsPureMappingFunction(methodSymbol) ||
                    SymbolHelper.IsUpdateThisObjectFunction(methodSymbol) ||
                    SymbolHelper.IsUpdateParameterFunction(methodSymbol) ||
                    SymbolHelper.IsMappingConstructor(methodSymbol)
                    )
                {

                    var diagnostic = Diagnostic.Create(MappingMethodRule, methodNode.Identifier.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
