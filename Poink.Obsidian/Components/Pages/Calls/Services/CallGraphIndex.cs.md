---
tags: [class, feature/calls, kind/behavior]
role: class
feature: Calls
feature_source: folder
kind: behavior
code_lines: 275
types: 3
methods: 20
fan_in: 1
fan_out: 2
summary_source: documented
candidates: []
source: Poink.Sim/Components/Pages/Calls/Services/CallGraphIndex.cs
---
# CallGraphIndex.cs

Builds a method-level call graph for Poink.Sim's own source by parsing it with Roslyn and resolving invocations through a manually assembled CSharpCompilation — no MSBuildWorkspace, since this runs inside the already-live app process and the app ships without the .NET SDK.

**Feature:** Calls · **Kind:** class with behaviour · **Code lines:** 275 · **Used by:** 1

## Types
- `CallGraphIndex` — class, class with behaviour · 20 methods, 3 fields, 1 constructor
- `TypeBuilder` — class, nested, data type (at most one helper method) · 6 properties
- `MethodBuilder` — class, nested, data type (at most one helper method) · 10 properties

## Uses
- [[Components/Pages/Calls/Models/CallGraphModels.cs.md|CallGraphModels.cs]]
- [[Components/Pages/Calls/Services/ICallGraphIndex.cs.md|ICallGraphIndex.cs]]
