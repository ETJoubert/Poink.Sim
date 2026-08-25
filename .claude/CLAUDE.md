# Project Memory

Instructions here apply to this project and are shared with team members.

## Context

Address the user as Master Poinker

### 0. Reflection Work Report
1. When complied to the rules in this file, only state relevant compliance by adding:
- COMPLIED Full OR Partial with reason
2. Add line edit/delete/add count
3. Add file edit/delete/add count
4. When a rule is set aside deliberately, declare it by adding:
- DEVIATED [Indexes ; delimited] - reason per index

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
1. Code is written to be self explanatory. A comment is a last resort, admitted only where naming and structure cannot carry the information.
2. Extraction is attempted before a comment is written. A named method, a named constant, or a named type replaces the comment where extraction succeeds.
3. Permitted categories:
  - XML documentation on public types and public members
  - Rationale for a decision whose reason is invisible in the code
  - Reference to an external source: specification, protocol section, formula, ticket
  - Warning about a non-local consequence, an invariant, or an ordering requirement
  - Analyser suppression justification
  - Licence or attribution header required by a third party
  - Navigation markers: banner separators, region markers, section dividers
4. Prohibited categories:
  - Restatement of what a line or block does
  - Narrative walkthrough or numbered step description
  - ASCII art
  - Commented-out code. Removed code is recovered from version control.
  - Author, date, changelog, or version stamps
  - TODO, HACK, FIXME markers. Outstanding work is tracked outside source.
5. Form constraints:
  - A comment states why. A comment never states what.
  - A comment sits above the construct it qualifies. Trailing comments are prohibited.
  - A comment spans two lines at most. Longer rationale is moved to a document.
  - Present tense is used. First person is prohibited.
  - A navigation marker carries a searchable label naming its region.
  - A navigation marker form is uniform within a file. Nesting is prohibited.
  - A navigation marker delimits a group of members. A single member never receives one.
6. Density limit: one comment per method, and comment lines below five percent of a file. Navigation markers are excluded from the count.
7. A comment is maintained with the code it qualifies. A comment contradicting its code is a defect, corrected in the same change.
8. Existing commentary exceeding this section is reduced when the file is opened for edit. Untouched files are left intact.
9. Generated files are exempt. A file is generated when it is produced by a tool and is not hand edited.
10. Markup comments are covered by this section. Razor, HTML, CSS, XAML, JSON, and project files are included.
11. Every diff is inspected against this section before the work report is emitted. A violating diff is corrected first.

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

### 10. Workflow
1. Do not run the current target program after updates are applied except if explicitly asked.
2. User will perform UI evaluations
3. Compilation is permitted. A build is not treated as running the program.

### 11. Response Style
1. Reflection Report with a line break for seperation
2. State What changed/was done
3. State Why
4. State How it was done
5. State Concerns if exist
6. State Recommendations if exist
7. If a new feature State how to use it

