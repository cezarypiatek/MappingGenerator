using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
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
            var mappingExpressions = GenerateMappingCode(methodSymbol, generator);
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(methodSyntax.Body, ((BaseMethodDeclarationSyntax)generator.MethodDeclaration(methodSymbol, mappingExpressions)).Body);
            return document.WithSyntaxRoot(newRoot);
        }

        private static IEnumerable<SyntaxNode> GenerateMappingCode(IMethodSymbol methodSymbol, SyntaxGenerator generator)
        {
            if (SymbolHelper.IsPureMappingFunction(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var targetType = methodSymbol.ReturnType;
                return MapTypes(source.Type, targetType, generator,generator.IdentifierName(source.Name));
            }

            if (SymbolHelper.IsUpdateThisObjectFunction(methodSymbol) || SymbolHelper.IsMappingConstructor(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var targetType = methodSymbol.ContainingType;
                return MapTypes(source.Type, targetType, generator, generator.IdentifierName(source.Name), generator.ThisExpression(), true);
            }

            if (SymbolHelper.IsUpdateParameterFunction(methodSymbol))
            {
                var source = methodSymbol.Parameters[0];
                var target = methodSymbol.Parameters[1];
                return MapTypes(source.Type, target.Type, generator, generator.IdentifierName(source.Name), generator.IdentifierName(target.Name), true);
            }

            return Enumerable.Empty<SyntaxNode>();
        }

        private static IEnumerable<SyntaxNode> MapTypes(ITypeSymbol sourceType, ITypeSymbol targetType, SyntaxGenerator generator, SyntaxNode globalSourceAccessor, SyntaxNode globbalTargetAccessor=null, bool targetExists = false)
        {
            var sourceClassSymbol = sourceType as INamedTypeSymbol;
            var targetClassSymbol = targetType as INamedTypeSymbol;
            if (sourceClassSymbol == null || targetClassSymbol == null)
            {
                yield break ;
            }

            if (IsMappingBetweenCollections(targetClassSymbol, sourceClassSymbol))
            {
                var collectionMapping = MapCollections(generator, globalSourceAccessor, sourceClassSymbol, targetClassSymbol);
                if (globbalTargetAccessor == null)
                {
                    yield return generator.ReturnStatement(collectionMapping);    
                }
                else if(targetExists == false)
                {
                    yield return generator.AssignmentStatement(globbalTargetAccessor, collectionMapping);
                }
                yield break;
            }
            
            var targetLocalVariableName = globbalTargetAccessor ==null? ToLocalVariableName(targetClassSymbol.Name): ToLocalVariableName(globbalTargetAccessor.ToFullString());
            if (targetExists == false)
            {
                var copyConstructor = FindCopyConstructor(targetClassSymbol, sourceClassSymbol);
                if (copyConstructor != null)
                {
                    var init = generator.ObjectCreationExpression(targetClassSymbol, globalSourceAccessor);
                    yield return generator.ReturnStatement(init);
                    yield break;
                }
                else
                {
                    var init = generator.ObjectCreationExpression(targetClassSymbol);
                    yield return generator.LocalDeclarationStatement(targetLocalVariableName, init);     
                }
            }


            var matchedProperties = RetrieveMatchedProperties(sourceClassSymbol, targetClassSymbol).ToList();
            //Direct 1-1 mapping
            var localTargetIdentifier = targetExists? globbalTargetAccessor: generator.IdentifierName(targetLocalVariableName);
            foreach (var x in matchedProperties.Where(x => x.Source != null  && x.Target != null))
            {
                if (x.Target.SetMethod.DeclaredAccessibility != Accessibility.Public && globbalTargetAccessor.Kind() != SyntaxKind.ThisExpression)
                {
                      continue;
                }

               if (IsMappingBetweenCollections(x.Target.Type, x.Source.Type))
                {
                    var targetAccess = generator.MemberAccessExpression(localTargetIdentifier, x.Target.Name);
                    var sourceAccess = generator.MemberAccessExpression(globalSourceAccessor, x.Source.Name);
                    var collectionMapping = MapCollections(generator, sourceAccess, (INamedTypeSymbol) x.Source.Type, (INamedTypeSymbol) x.Target.Type);
                    yield return generator.AssignmentStatement(targetAccess, collectionMapping);
                }
                else if (IsSimpleType(x.Target.Type) == false)
                {   
                    //TODO: What if both sides has the same type?
                    //TODO: What if source is  byte[]
                    //TODO: Reverse flattening
                    var targetAccess = generator.MemberAccessExpression(localTargetIdentifier, x.Target.Name);
                    var sourceAccess = generator.MemberAccessExpression(globalSourceAccessor, x.Source.Name);
                    foreach (var complexPropertyMappingNode in MapTypes(x.Source.Type, x.Target.Type, generator, sourceAccess, targetAccess))
                    {
                        yield return complexPropertyMappingNode;
                    }
                }
                else
                {
                    //TODO: What if boths sides has different type? 
                    // - Source is a wrapper of target (has only one property that match the target type)
                    // - There is another type of conversion between source and target
                    var targetAccess = generator.MemberAccessExpression(localTargetIdentifier, x.Target.Name);
                    var sourceAccess = generator.MemberAccessExpression(globalSourceAccessor, x.Source.Name);
                    yield return generator.AssignmentStatement(targetAccess, sourceAccess);
                }
            }

            //Non-direct (mapping like y.UserName = x.User.Name)
            var unmappedDirectly = matchedProperties.Where(x => x.Source != null  && x.Target == null);
            foreach (var unmapped in unmappedDirectly)
            {
                var targetsWithPartialNameAsSource = matchedProperties.Where(x => x.Target != null && x.Target.Name.StartsWith(unmapped.Source.Name)).Select(x => x.Target);
                var sourceProperties = GetPublicPropertySymbols(unmapped.Source.Type as INamedTypeSymbol).ToList();
                var sourceFirstLevelAccess = generator.MemberAccessExpression(globalSourceAccessor, unmapped.Source.Name);
                foreach (var target in targetsWithPartialNameAsSource)
                {   
                    foreach (var sourceProperty in sourceProperties)
                    {
                        if (target.Name == $"{unmapped.Source.Name}{sourceProperty.Name}")
                        {
                            var targetAccess = generator.MemberAccessExpression(localTargetIdentifier, target.Name);
                            var sourceAccess = generator.MemberAccessExpression(sourceFirstLevelAccess, sourceProperty.Name);
                            var assigment = generator.AssignmentStatement(targetAccess, sourceAccess);
                            yield return assigment;
                        }
                    }
                }
            }

            //Flattening with function eg. t.Total = s.GetTotal()
            var unmappedDirectlySourcesMethod = matchedProperties.Where(x => x.SourceGetMethod != null  && x.Target == null);
            foreach (var unmappedSourceMethod in unmappedDirectlySourcesMethod)
            {
                var target = matchedProperties.FirstOrDefault(x => x.Target!=null && unmappedSourceMethod.SourceGetMethod.Name.EndsWith(x.Target.Name))?.Target;
                if (target != null)
                {
                    if (target.SetMethod.DeclaredAccessibility != Accessibility.Public && globbalTargetAccessor.Kind() != SyntaxKind.ThisExpression)
                    {
                        continue;
                    }

                    var targetAccess = generator.MemberAccessExpression(localTargetIdentifier, target.Name);
                    var sourceAccess = generator.MemberAccessExpression(globalSourceAccessor, unmappedSourceMethod.SourceGetMethod.Name);
                    var sourceCall = generator.InvocationExpression(sourceAccess);
                    yield return generator.AssignmentStatement(targetAccess, sourceCall);
                }
            }


            if (globbalTargetAccessor == null)
            {
                yield return generator.ReturnStatement(localTargetIdentifier);    
            }
            else if(targetExists == false)
            {
                yield return generator.AssignmentStatement(globbalTargetAccessor, localTargetIdentifier);
            }
        }

        private static SyntaxNode MapCollections(SyntaxGenerator generator, SyntaxNode sourceAccess, INamedTypeSymbol sourceListType, INamedTypeSymbol targetListType)
        {
            var isReadolyCollection = targetListType.Name == "ReadOnlyCollection";
            var sourceListElementType = sourceListType.TypeArguments[0];
            var targetListElementType = targetListType.TypeArguments[0];
            if (IsSimpleType(sourceListElementType))
            {
                var toListAccess = generator.MemberAccessExpression(sourceAccess, "ToList");
                var toListInvocation = generator.InvocationExpression(toListAccess);
                return WrapInReadonlyyCollectionIfNecessary(isReadolyCollection, toListInvocation, generator);
            }
            var converAccess = generator.MemberAccessExpression(sourceAccess, "ConvertAll");
            var lambdaParameterName = ToSingularLocalVariableName(ToLocalVariableName(sourceListElementType.Name));
            var listElementMappingStms = MapTypes(sourceListElementType, targetListElementType, generator, generator.IdentifierName(lambdaParameterName));
            var convertInvocation = generator.InvocationExpression(converAccess, generator.ValueReturningLambdaExpression(lambdaParameterName, listElementMappingStms));
            return WrapInReadonlyyCollectionIfNecessary(isReadolyCollection, convertInvocation, generator);
        }

        private static SyntaxNode WrapInReadonlyyCollectionIfNecessary(bool isReadonly, SyntaxNode node, SyntaxGenerator generator)
        {
            if (isReadonly == false)
            {
                return node;
            }

            var accessAsReadonly = generator.MemberAccessExpression(node, "AsReadOnly");
            return generator.InvocationExpression(accessAsReadonly);
        }

        private static bool IsMappingBetweenCollections(ITypeSymbol targetClassSymbol, ITypeSymbol sourceClassSymbol)
        {
            return HasInterface(targetClassSymbol, "System.Collections.Generic.ICollection<T>") && HasInterface(sourceClassSymbol, "System.Collections.Generic.IEnumerable<T>");
        }

        private static IMethodSymbol FindCopyConstructor(INamedTypeSymbol type, INamedTypeSymbol constructorParameterType)
        {
            return type.Constructors.FirstOrDefault(c => c.Parameters.Length == 1 && c.Parameters[0].Type == constructorParameterType);
        }

        private static string[] SimpleTypes = new[] {"String", "Decimal"};

        private static bool IsSimpleType(ITypeSymbol type)
        {
            return type.IsValueType || SimpleTypes.Contains(type.Name);
        }

        private static char[] FobiddenSigns = new[] {'.', '[', ']', '(', ')'};

        private static string ToLocalVariableName(string proposalLocalName)
        {
            var withoutForbiddenSigns = string.Join("",proposalLocalName.Trim().Split(FobiddenSigns).Select(x=>  $"{x.Substring(0, 1).ToUpper()}{x.Substring(1)}"));
            return $"{withoutForbiddenSigns.Substring(0, 1).ToLower()}{withoutForbiddenSigns.Substring(1)}";
        } 
        
        private static string ToSingularLocalVariableName(string proposalLocalName)
        {
            if (proposalLocalName.EndsWith("s"))
            {
                return proposalLocalName.Substring(0, proposalLocalName.Length - 1);
            }

            return proposalLocalName;
        }

        private static bool HasInterface(ITypeSymbol xt, string interfaceName)
        {
            return xt.OriginalDefinition.AllInterfaces.Any(x => x.ToDisplayString() == interfaceName);
        }

        private static bool IsPublicGetMethod(ISymbol x)
        {
           return x is IMethodSymbol mSymbol
                   && mSymbol.ReturnsVoid == false
                   && mSymbol.Parameters.Length == 0
                   && x.DeclaredAccessibility == Accessibility.Public
                   && x.IsImplicitlyDeclared == false
                   && mSymbol.MethodKind == MethodKind.Ordinary
                ;
        }

        private static bool IsPublicPropertySymbol(ISymbol x)
        {
            if (x.Kind != SymbolKind.Property)
            {
                return false;
            }

            if (x is IPropertySymbol mSymbol)
            {
                if (mSymbol.IsStatic || mSymbol.IsIndexer || mSymbol.DeclaredAccessibility != Accessibility.Public)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static IEnumerable<IPropertySymbol> GetPublicPropertySymbols(INamedTypeSymbol source)
        {
            return GetBaseTypesAndThis(source).SelectMany(x=> x.GetMembers()).Where(IsPublicPropertySymbol).OfType<IPropertySymbol>();
        }   
        
        private static IEnumerable<IMethodSymbol> GetPublicGetMethods(INamedTypeSymbol source)
        {
            return GetBaseTypesAndThis(source).SelectMany(x=> x.GetMembers()).Where(IsPublicGetMethod).OfType<IMethodSymbol>();
        }

        private  static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(INamedTypeSymbol type)
        {
            var current = type;
            while (current != null)
            {
                yield return current;
                current = current.BaseType;
            }
        }
        
        private static IEnumerable<MatchedPropertySymbols> RetrieveMatchedProperties(INamedTypeSymbol source, INamedTypeSymbol target)
        {
            var propertiesMap = new SortedDictionary<string, MatchedPropertySymbols>();

            foreach (var mSymbol in GetPublicPropertySymbols(source))
            {
                if (!propertiesMap.ContainsKey(mSymbol.Name))
                {
                    // If class definition is invalid, it may happen that we get multiple properties with the same name
                    // Ignore all but first
                    propertiesMap.Add(mSymbol.Name, new MatchedPropertySymbols() { Source = mSymbol });
                }
            }

            foreach (var methodSymbol in GetPublicGetMethods(source))
            {
                if (propertiesMap.ContainsKey(methodSymbol.Name) == false)
                {
                    propertiesMap.Add(methodSymbol.Name, new MatchedPropertySymbols()
                    {
                        SourceGetMethod = methodSymbol
                    });
                }
            }

            foreach (var mSymbol in GetPublicPropertySymbols(target))
            {
                if (mSymbol.IsReadOnly)
                {
                    continue;
                }

                MatchedPropertySymbols sourceProperty = null;
                if (!propertiesMap.TryGetValue(mSymbol.Name, out sourceProperty))
                {
                    propertiesMap.Add(mSymbol.Name, new MatchedPropertySymbols { Target = mSymbol });
                }
                else if (sourceProperty.Target == null)
                {
                    // If class definition is invalid, it may happen that we get multiple properties with the same name
                    // Ignore all but first
                    sourceProperty.Target = mSymbol;
                }
            }

            return propertiesMap.Values;
        }
    }

    internal class MatchedPropertySymbols
    {
        public IPropertySymbol Source { get; set; }
        public IMethodSymbol SourceGetMethod { get; set; }
        public IPropertySymbol Target { get; set; }
    }
}
