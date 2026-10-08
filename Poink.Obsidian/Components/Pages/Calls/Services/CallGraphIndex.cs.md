---
tags: [class]
source: Poink.Sim/Components/Pages/Calls/Services/CallGraphIndex.cs
---
# CallGraphIndex.cs

Builds a method-level call graph for Poink.Sim's own source by parsing it with Roslyn and resolving invocations through a manually assembled CSharpCompilation — no MSBuildWorkspace, since this runs inside the already-live app process and the app ships without the .NET SDK.

**Used by:** 1 file (see Backlinks)

## Uses
- [[Components/Pages/Calls/Models/CallGraphModels.cs.md|CallGraphModels.cs]]
- [[Components/Pages/Calls/Services/ICallGraphIndex.cs.md|ICallGraphIndex.cs]]
