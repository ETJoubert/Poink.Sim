using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Poink.Analyzers.Helpers;

/// <summary>Shared trivia classification for the CLAUDE.md 4.x comment-style analyzers.</summary>
public static class CommentTriviaHelper
{
    private static readonly Regex NavigationMarkerPattern = new(@"^[-=#*~]{3,}|[-=#*~]{3,}$", RegexOptions.Compiled);
    private static readonly Regex MarkerWordPattern = new(@"\b(TODO|HACK|FIXME)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AuthorOrDateStampPattern = new(
        @"\b(author|@author|changelog|change\s*log)\b|\b(created|modified|updated|date|version)\s*:",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsRegularComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);

    public static bool IsDocComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

    public static string GetCommentBody(SyntaxTrivia trivia)
    {
        string text = trivia.ToFullString();
        if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            return text.Length >= 2 ? text.Substring(2) : string.Empty;

        if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
        {
            string inner = text;
            if (inner.StartsWith("/*", System.StringComparison.Ordinal))
                inner = inner.Substring(2);
            if (inner.EndsWith("*/", System.StringComparison.Ordinal))
                inner = inner.Substring(0, inner.Length - 2);
            return inner;
        }

        return text;
    }

    public static bool IsNavigationMarker(string commentBody) => NavigationMarkerPattern.IsMatch(commentBody.Trim());

    public static bool ContainsMarkerWord(string commentBody, out string marker)
    {
        Match match = MarkerWordPattern.Match(commentBody);
        marker = match.Success ? match.Value.ToUpperInvariant() : string.Empty;
        return match.Success;
    }

    public static bool LooksLikeAuthorOrDateStamp(string commentBody) => AuthorOrDateStampPattern.IsMatch(commentBody);

    public static int LineSpan(SyntaxTrivia trivia, SyntaxTree tree)
    {
        FileLinePositionSpan span = tree.GetLineSpan(trivia.Span);
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }
}
