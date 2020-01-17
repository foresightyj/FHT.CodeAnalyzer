using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FHT.CodeAnalyzer
{
    public static class RoslynExtensions
    {
        public static string StringValue(this LiteralExpressionSyntax node)
        {
            if (node.Kind() != SyntaxKind.StringLiteralExpression) throw new ArgumentException("node is not string literal");
            var str = node.Token.ToString();
            if (str.StartsWith("@"))
            {
                str = str.Substring(1);
            }
            str = str.Substring(1, str.Length - 2);
            return str;
        }
    }
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StringLiteralCheckAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FHT_StringLiteralCheck";
        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private const string Category = "StringLiteralCheck";
        private const string Title = "Prefer nameof or predefined consts/variables over string literals";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId,
            Title,
            "Prefer using nameof or string.Empty other than literal string: {0}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: "This is better");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(CheckStringLiteralInMethodArguments, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(CheckComparisonWithChineseString, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        }

        private static void CheckComparisonWithChineseString(SyntaxNodeAnalysisContext ctx)
        {
            var binary = (BinaryExpressionSyntax)ctx.Node;
            var literals = new[] { binary.Left, binary.Right }.OfType<LiteralExpressionSyntax>();
            foreach (var literal in literals)
            {
            }
        }

        private static void CheckStringLiteralInMethodArguments(SyntaxNodeAnalysisContext ctx)
        {
            //see https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix
            //also https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Samples.md
            var invocation = (InvocationExpressionSyntax)ctx.Node;

            var interested = new List<(string patt, int pos)>
            {
                (@"ModelState\.AddModelError$", 0),
                (@"\.AbsoluteAction$", 0),
                (@"\.Action$", 0),
                (@"RedirectToAction$", 0),
            };

            var exString = invocation.Expression.ToString();
            string violation = null;
            foreach (var i in interested)
            {
                if (Regex.IsMatch(exString, i.patt))
                {
                    var arg = invocation.ArgumentList.Arguments.Skip(i.pos).FirstOrDefault();
                    if (arg != null)
                    {
                        if (arg.Expression is LiteralExpressionSyntax s && s.Kind() == SyntaxKind.StringLiteralExpression)
                        {
                            if (!string.IsNullOrEmpty(s.StringValue()))
                            {
                                violation = s.ToString();
                                break;
                            }
                        }
                        else if (arg.Expression is InterpolatedStringExpressionSyntax ise)
                        {
                            violation = ise.ToString();
                            break;
                        }
                    }
                }
            }
            if (violation != null)
            {
                var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), violation);
                ctx.ReportDiagnostic(diagnostic);
            }
        }
    }
}

