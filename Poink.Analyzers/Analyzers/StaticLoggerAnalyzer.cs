using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Poink.Analyzers.Diagnostics;

namespace Poink.Analyzers.Analyzers;

/// <summary>Flags static Serilog Log.* calls; CLAUDE.md 9.3 requires loggers consumed through injected ILogger&lt;T&gt;.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticLoggerAnalyzer : DiagnosticAnalyzer
{
    private const string SerilogStaticLoggerType = "Serilog.Log";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(PoinkDiagnostics.StaticLoggerCall);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.Invocation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol method = invocation.TargetMethod;

        if (!method.IsStatic || method.ContainingType?.ToDisplayString() != SerilogStaticLoggerType)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            PoinkDiagnostics.StaticLoggerCall,
            invocation.Syntax.GetLocation(),
            $"{SerilogStaticLoggerType}.{method.Name}"));
    }
}
