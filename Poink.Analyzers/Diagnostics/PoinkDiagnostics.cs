using Microsoft.CodeAnalysis;

namespace Poink.Analyzers.Diagnostics;

/// <summary>Diagnostic descriptors for the CLAUDE.md rules enforced by this analyzer.</summary>
public static class PoinkDiagnostics
{
    private const string CommentsCategory = "Poink.Comments";
    private const string LoggingCategory = "Poink.Logging";
    private const string NamingCategory = "Poink.Naming";

    public static readonly DiagnosticDescriptor MarkerComment = new(
        id: "POINK0001",
        title: "TODO/HACK/FIXME marker in comment",
        messageFormat: "Comment contains a '{0}' marker; outstanding work is tracked outside source (CLAUDE.md 4.5)",
        category: CommentsCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TrailingComment = new(
        id: "POINK0002",
        title: "Trailing comment",
        messageFormat: "Comment trails code on the same line; a comment must sit above the construct it qualifies (CLAUDE.md 4.5)",
        category: CommentsCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AuthorOrDateStamp = new(
        id: "POINK0003",
        title: "Author/date/changelog stamp in comment",
        messageFormat: "Comment looks like an author, date, changelog, or version stamp, which is prohibited (CLAUDE.md 4.4)",
        category: CommentsCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CommentTooLong = new(
        id: "POINK0004",
        title: "Comment exceeds two lines",
        messageFormat: "Comment spans more than two lines; longer rationale belongs in a document (CLAUDE.md 4.5)",
        category: CommentsCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TooManyCommentsInMethod = new(
        id: "POINK0005",
        title: "More than one comment in a method",
        messageFormat: "Method '{0}' carries {1} separate comments; the density limit is one comment per method (CLAUDE.md 4.6)",
        category: CommentsCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CommentDensityExceeded = new(
        id: "POINK0006",
        title: "File comment density exceeds 5%",
        messageFormat: "File comment density is {0:P1}, above the 5% limit (CLAUDE.md 4.6)",
        category: CommentsCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StaticLoggerCall = new(
        id: "POINK0007",
        title: "Static Serilog Log call",
        messageFormat: "'{0}' is a static Serilog call; loggers are consumed through injected ILogger<T> (CLAUDE.md 9.3)",
        category: LoggingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingRoleSuffix = new(
        id: "POINK0008",
        title: "Class missing role suffix",
        messageFormat: "Class '{0}' implements '{1}' but its name does not end with '{2}' (CLAUDE.md 1.3)",
        category: NamingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
