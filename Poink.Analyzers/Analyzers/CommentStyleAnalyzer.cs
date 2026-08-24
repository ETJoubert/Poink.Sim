using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Poink.Analyzers.Diagnostics;
using Poink.Analyzers.Helpers;

namespace Poink.Analyzers.Analyzers;

/// <summary>Enforces the CLAUDE.md 4.x comment-style rules: markers, trailing placement, stamps, length, and density.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommentStyleAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        PoinkDiagnostics.MarkerComment,
        PoinkDiagnostics.TrailingComment,
        PoinkDiagnostics.AuthorOrDateStamp,
        PoinkDiagnostics.CommentTooLong,
        PoinkDiagnostics.TooManyCommentsInMethod,
        PoinkDiagnostics.CommentDensityExceeded);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        SyntaxTree tree = context.Tree;
        SyntaxNode root = tree.GetRoot(context.CancellationToken);
        List<CommentItem> items = CollectCommentItems(root);

        var commentLines = new HashSet<int>();
        foreach (CommentItem item in items)
            ReportItem(context, tree, item, commentLines);

        ReportDensity(context, tree, commentLines.Count);
        ReportMethodDensity(context, root, items);
    }

    private static void ReportItem(SyntaxTreeAnalysisContext context, SyntaxTree tree, CommentItem item, HashSet<int> commentLines)
    {
        string body = item.CombinedBody();
        bool isNavigationMarker = CommentTriviaHelper.IsNavigationMarker(body);
        Location location = Location.Create(tree, item.Span);

        if (!isNavigationMarker)
        {
            FileLinePositionSpan span = tree.GetLineSpan(item.Span);
            for (int line = span.StartLinePosition.Line; line <= span.EndLinePosition.Line; line++)
                commentLines.Add(line);
        }

        if (item.IsTrailing)
            context.ReportDiagnostic(Diagnostic.Create(PoinkDiagnostics.TrailingComment, location));

        if (CommentTriviaHelper.ContainsMarkerWord(body, out string marker))
            context.ReportDiagnostic(Diagnostic.Create(PoinkDiagnostics.MarkerComment, location, marker));

        if (isNavigationMarker)
            return;

        if (CommentTriviaHelper.LooksLikeAuthorOrDateStamp(body))
            context.ReportDiagnostic(Diagnostic.Create(PoinkDiagnostics.AuthorOrDateStamp, location));

        if (item.LineCount(tree) > 2)
            context.ReportDiagnostic(Diagnostic.Create(PoinkDiagnostics.CommentTooLong, location));
    }

    private static void ReportDensity(SyntaxTreeAnalysisContext context, SyntaxTree tree, int commentLineCount)
    {
        int totalLines = tree.GetText(context.CancellationToken).Lines.Count;
        if (totalLines == 0)
            return;

        double density = (double)commentLineCount / totalLines;
        if (density > 0.05)
            context.ReportDiagnostic(Diagnostic.Create(
                PoinkDiagnostics.CommentDensityExceeded, Location.Create(tree, new TextSpan(0, 0)), density));
    }

    private static void ReportMethodDensity(SyntaxTreeAnalysisContext context, SyntaxNode root, List<CommentItem> items)
    {
        foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            int count = items.Count(item =>
                !CommentTriviaHelper.IsNavigationMarker(item.CombinedBody()) && method.Span.Contains(item.Span));

            if (count > 1)
                context.ReportDiagnostic(Diagnostic.Create(
                    PoinkDiagnostics.TooManyCommentsInMethod, method.Identifier.GetLocation(), method.Identifier.Text, count));
        }
    }

    private static List<CommentItem> CollectCommentItems(SyntaxNode root)
    {
        var items = new List<CommentItem>();
        foreach (SyntaxToken token in root.DescendantTokens())
        {
            CollectFromLeadingTrivia(token.LeadingTrivia, items);
            foreach (SyntaxTrivia trivia in token.TrailingTrivia)
            {
                if (CommentTriviaHelper.IsRegularComment(trivia))
                    items.Add(CommentItem.Single(trivia, isTrailing: true));
            }
        }

        return items;
    }

    private static void CollectFromLeadingTrivia(SyntaxTriviaList triviaList, List<CommentItem> items)
    {
        List<SyntaxTrivia>? currentGroup = null;
        int consecutiveNewlines = 0;

        foreach (SyntaxTrivia trivia in triviaList)
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                consecutiveNewlines++;
                if (consecutiveNewlines >= 2)
                    FlushGroup(ref currentGroup, items);
                continue;
            }

            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                continue;

            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                currentGroup ??= new List<SyntaxTrivia>();
                currentGroup.Add(trivia);
                consecutiveNewlines = 0;
                continue;
            }

            FlushGroup(ref currentGroup, items);
            consecutiveNewlines = 0;

            if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                items.Add(CommentItem.Single(trivia, isTrailing: false));
        }

        FlushGroup(ref currentGroup, items);
    }

    private static void FlushGroup(ref List<SyntaxTrivia>? group, List<CommentItem> items)
    {
        if (group is { Count: > 0 })
            items.Add(new CommentItem(group, isTrailing: false));
        group = null;
    }

    private sealed class CommentItem
    {
        private readonly List<SyntaxTrivia> _trivia;

        public CommentItem(List<SyntaxTrivia> trivia, bool isTrailing)
        {
            _trivia = trivia;
            IsTrailing = isTrailing;
        }

        public bool IsTrailing { get; }

        public TextSpan Span => TextSpan.FromBounds(_trivia[0].SpanStart, _trivia[_trivia.Count - 1].Span.End);

        public static CommentItem Single(SyntaxTrivia trivia, bool isTrailing) => new(new List<SyntaxTrivia> { trivia }, isTrailing);

        public string CombinedBody()
        {
            var builder = new StringBuilder();
            foreach (SyntaxTrivia trivia in _trivia)
                builder.AppendLine(CommentTriviaHelper.GetCommentBody(trivia));
            return builder.ToString();
        }

        public int LineCount(SyntaxTree tree)
        {
            FileLinePositionSpan span = tree.GetLineSpan(Span);
            return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
        }
    }
}
