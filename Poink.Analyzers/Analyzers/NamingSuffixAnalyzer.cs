using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Poink.Analyzers.Diagnostics;

namespace Poink.Analyzers.Analyzers;

/// <summary>Flags a class implementing an I*Suffix interface without carrying that role suffix itself (CLAUDE.md 1.3).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NamingSuffixAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] RoleSuffixes = { "Service", "Repository", "Helper" };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(PoinkDiagnostics.MissingRoleSuffix);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class)
            return;

        foreach (INamedTypeSymbol iface in type.Interfaces)
        {
            string? suffix = RoleSuffixFor(iface.Name);
            if (suffix is null || type.Name.EndsWith(suffix, System.StringComparison.Ordinal))
                continue;

            Location location = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                PoinkDiagnostics.MissingRoleSuffix, location, type.Name, iface.Name, suffix));
        }
    }

    private static string? RoleSuffixFor(string interfaceName)
    {
        if (interfaceName.Length < 2 || interfaceName[0] != 'I' || !char.IsUpper(interfaceName[1]))
            return null;

        foreach (string suffix in RoleSuffixes)
        {
            if (interfaceName.EndsWith(suffix, System.StringComparison.Ordinal) && interfaceName.Length > suffix.Length + 1)
                return suffix;
        }

        return null;
    }
}
