using System;
using System.Collections.Generic;

namespace Poink.Sim.Components.Pages.Calls.Models;

public sealed record CallLocation(string FilePath, int Line);

public sealed record CallGraphEdge(string MethodId, string Signature, string TypeName, CallLocation Site);

public sealed record CallGraphMethod(
    string Id,
    string Signature,
    CallLocation DeclaredAt,
    IReadOnlyList<CallGraphEdge> Outgoing,
    IReadOnlyList<CallGraphEdge> Incoming);

public sealed record CallGraphType(
    string Id,
    string Name,
    string Namespace,
    string FilePath,
    string Kind,
    IReadOnlyList<CallGraphMethod> Methods);

public sealed record CallGraphStats(
    int TypeCount,
    int MethodCount,
    int EdgeCount,
    TimeSpan BuildDuration,
    int DiagnosticCount);

public sealed record CallGraphIndexResult(IReadOnlyList<CallGraphType> Types, CallGraphStats Stats);
