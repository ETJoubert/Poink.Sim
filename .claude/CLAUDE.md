# Project Memory

Instructions here apply to this project and are shared with team members.

## Context

Address the user as Master Poinker

### 0. Response Report
Every response ends with this report. Each section label is bold, sections are separated by a line break. Compliance and Concerns are prefixed with ✅ for a good state and ❌ for a bad one.
1. **Abstract:** a one-line TLDR of the work.
2. **Compliance:** ✅ COMPLIED Full or Partial (with reason), or ❌ DEVIATED [indexes ; delimited] with a reason per index.
3. **Git:** the current git status.
4. **Details:** short What changed, Why, and How it was done: Each in sub-section label is italic.
5. **Usage:** how to use it, when the work is a new feature.
6. **Concerns:** ✅ None, or ❌ followed by each concern.
7. **Recommendations:** state if any exist.
8. **Finally:** State Next workflow steps

### 10. Workflow
1. Master is the core branch. Its own working tree stays on master; feature work never checks out another branch there.
2. Ask if a feature branch should be created. If yes, create the branch in its own worktree at a sibling path (`../Poink.Sim-<branch-name>`), so multiple branches can be iterated on in parallel without checkout conflicts.
3. Build and iterate inside that worktree.
4. Compilation is permitted. A build is not treated as running the program.
5. Do not run the current target program after updates are applied except if explicitly asked.
6. When done, do not commit; provide the Response Report (§0).
7. When prompted to merge: commit in the worktree, merge the branch into master, then remove the worktree and delete the merged branch.
8. Pushing to origin is for backup or PR review, not for handing work between local instances; parallel local work is coordinated through worktrees, not through push/pull round-trips.

### 1. Architecture & Organization
1. Build new things modular, for clear, scalable code.
  - SOLID principles
  - Dependency Injection
  - Separation of Concerns
  - Consistent, self-descriptive naming
2. Where possible create directories to hold related files like:
  - Pages and Components
  - Interfaces and Services
  - Repositories
  - Models, records and Dtos
  - Helpers
  - Utilities/Tools (Methods that are I/O to be created as objects for general use grouped by purpose)
3. Apply naming conventions by appending the type names like:
  - ThingService
  - *ThingModels (Containing Thing Models,dto and records)
  - ThingHelper
  - ThingPage
  - ThingComponent


### 2. UI
1. Always add Tooltips to any control on the UI.

### 3. Change Management
1. Keep existing functionality except for when explicitly asked that it should be replaced.
2. If existing functionality cant be kept, stop and ask.

### 4. Code Style
1. Code is self-explanatory. A comment is a last resort; extraction (a named method, constant, or type) is tried first.
2. A comment states why, never what. It sits above the construct it qualifies (never trailing), present tense, one line typically and two at most.
3. Permitted: XML docs on public members, rationale invisible in the code, external references (spec/ticket/formula), warnings about non-local consequences or ordering, analyser suppression justification, required licence headers, and uniform navigation markers (one per group, never nested, never on a single member).
4. Prohibited: restating what code does, narrative walkthroughs, ASCII art, commented-out code, author/date/changelog/version stamps, TODO/HACK/FIXME markers.
5. Markup comments (Razor, HTML, XAML, JSON, project files) follow the same rules. Generated files are exempt.
6. `Poink.Analyzers` (`CommentStyleAnalyzer`) is the source of truth for this section and enforces it at build time: POINK0001 marker words, POINK0002 trailing comments, POINK0003 author/date stamps, POINK0004 comments over two lines, POINK0005 more than one comment per method, POINK0006 file comment density over 5%. Treat any of these warnings as a violation to fix, not silence.

### 5. Tooling Constraints
1. Do not use playwright.

### 6. Documentation
1. 


### 7. Testing (Ignored for now)
1. Cover critical trading/strategy logic with unit tests before merging.
2. Do not change tested behavior without updating its tests.

### 8. Security
1. Never commit API keys, credentials, or secrets to the repository.
2. Store sensitive configuration (broker credentials, API keys) outside source control.

### 9. Logging
1. Log runtime state changes, including settings used and process start/end.
2. Logging should make it clear when something happened and what happened.
3. Serilog is the provider. Loggers are consumed through injected ILogger<T>. Static Log calls are prohibited.
4. Levels are assigned as:
  - Information: process start/end, settings applied, load/save/reset, repository calls
  - Warning: recoverable degradation, skipped work, fallback taken
  - Error: failed operation, always paired with the exception
  - Debug: developer diagnostics, excluded from the default minimum level
5. Per-tick, per-episode, and per-request events are excluded from logging.





