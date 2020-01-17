using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FHT.CodeAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InheritanceCheckAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FHT_InheritanceCheck";
        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private const string Category = "Inheritance";
        private const string IsPolymorphicBaseClassAttributeName = "IsPolymorphicBaseClassAttribute";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId,
            IsPolymorphicBaseClassAttributeName,
            "Type {0}'s base class must be annotated with " + IsPolymorphicBaseClassAttributeName,
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: "This is require for swagger generated models to work property");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext ctx)
        {
            //see https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix
            //also https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Samples.md
            var cls = (ClassDeclarationSyntax)ctx.Node;
            if (cls.BaseList == null) return;
            var className = cls.Identifier.ToString();
            var isUIModel = className.ToLower().Contains("inputmodel") || className.ToLower().Contains("viewmodel");
            if (!isUIModel) return;
            var baseNames = cls.BaseList.Types.Select(t => t.Type).OfType<IdentifierNameSyntax>().Select(i => i.Identifier.ToString());
            var hasBaseTypeOfBaseOrModel = baseNames.Any(n => n.ToLower().Contains("base") || n.ToLower().Contains("model"));
            if (!hasBaseTypeOfBaseOrModel) return;
            var baseType = ctx.SemanticModel.GetDeclaredSymbol(cls).BaseType;
            var isFHTNamespace = baseType.ContainingNamespace.ToString().ToLower().StartsWith("fht.");
            if (!isFHTNamespace) return;
                var baseTypeHasIsPolymorphicBaseClassAttribute = baseType.GetAttributes().Any(a => a.AttributeClass.Name == IsPolymorphicBaseClassAttributeName);
                if (!baseTypeHasIsPolymorphicBaseClassAttribute)
                {
                    var diagnostic = Diagnostic.Create(Rule, cls.GetLocation(), className);
                    ctx.ReportDiagnostic(diagnostic);
                }
        }
    }
}
