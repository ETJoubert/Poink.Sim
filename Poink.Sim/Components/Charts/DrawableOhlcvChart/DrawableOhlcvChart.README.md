# Adding a draw tool to DrawableOhlcvChart

Cheat sheet for adding a new click-to-draw tool (like T-Range Chart, Rectangle, or
T-Range Reference Chart). Follow the checklist in order — each step depends on the
identifier/name chosen in the step before it.

## The architecture, in one paragraph

Three layers, each with its **own copy** of "what draw modes exist" — there is no
shared enum/codegen, so every new tool touches all three by hand:

1. **`DrawableOhlcvChart.razor(.cs)`** — the C# `DrawMode` enum, the toolbar buttons,
   and the `[JSInvokable]`/`EventCallback` plumbing that turns chart clicks into
   strongly-typed callbacks.
2. **`wwwroot/js/drawableOhlcvChart.js`** — the actual click state machine
   (`entry.drawClicks`), which mode needs how many clicks (`modeClickCounts`), and
   what each click dispatches to .NET.
3. **The consuming page** (e.g. `ProfilerPage.razor(.cs)`) — owns the real `Drawing`
   model, decides a completed drawing's `Kind`/`ColorGroup`, and renders the
   side-panel card. `DrawableOhlcvChart` itself has no notion of "a drawing", only
   individual horizontal/vertical/diagonal lines and range fills.

## Checklist

### 1. `DrawableOhlcvChart.razor.cs`
- Add a member to the `DrawMode` enum.
- Map it to a lowercase JS string in `JsDrawMode()`.
- Decide: does the tool's data reduce to existing horizontal/vertical/diagonal line
  clicks (like T-Range/P-Range/Rectangle), or does it need its own payload?
  - **Reduces to existing lines** — nothing else needed here; the JS side will just
    call `OnVerticalLineClickedAsync`/`OnHorizontalLineClickedAsync` repeatedly.
  - **Needs its own payload** — add:
    - `[Parameter] public EventCallback<T> OnXxxDrawn { get; set; }`
    - `[JSInvokable] public Task OnXxxClickedAsync(...) => InvokeIfBound(OnXxxDrawn, ...)`
    - if `T` is a new shape, a `public readonly record struct ChartXxx(...)` next to
      `ChartHorizontalLine`/`ChartVerticalLine`/`ChartDiagonalLine`/`ChartTimeRange`/`ChartRangeFill`.

### 2. `DrawableOhlcvChart.razor`
- Add a `<button>` inside the matching `.doc-tool-group-*` div (Lines / Ranges /
  Shapes / Charts), or a new group if it genuinely doesn't fit one.
- Always give it a `title` tooltip describing the full click sequence (project rule:
  every control needs a tooltip).

### 3. `wwwroot/js/drawableOhlcvChart.js`
- `modeClickCounts`: add `<mode>: <total clicks>`.
- `subscribeClick`'s `switch (mode)`: add a case. For a multi-phase tool, branch on
  `clickIndex` (see `rectangle`'s "first 2 are time, last 2 are price" or
  `trangereferencechart`'s "first 2 are the range, 3rd is the reference").
- **Positional constraint on a click** (e.g. "must land before the earlier of the
  first two")? Check it **before** `entry.drawClicks.push(...)` and `return` early
  on failure — this leaves `drawClicks` untouched so the sequence doesn't silently
  advance on a rejected click, and the tool stays armed for another attempt.
- If the sequence needs one bundling callback once several points have all landed
  (like `OnTRangeChartClickedAsync` for T-Range Chart's 2 clicks), add it in the
  `if (entry.drawClicks.length < needed) { ...; return; }` completion block, after
  the per-click dispatch switch.
- `drawHint(mode, clickIndex)`: add a case with "click N of M"-style text for the
  *next* click still needed (`clickIndex` is 0-based and always refers to the click
  not yet placed). This drives the toolbar's "what to click next" hint
  (`OnDrawHintAsync`) — also send a hint on a rejected click explaining why it
  didn't count, so the user isn't left guessing.
- Want a colour distinct from the four existing groups (lines/ranges/shapes/charts)?
  Add an entry to `groupColors` sourced from an unused `--color-*` CSS var (see
  table below).

### 4. The consuming page's code-behind (e.g. `ProfilerPage.razor.cs`)
- `Drawing` record: add a new `List<ChartXxx> Xxxs` field if step 1 introduced new
  data. Update `NewPending()`'s constructor call to match the new arg count.
- `Kind` switch: add the new case **above** any existing case it could also match
  (order matters — a more specific pattern loses to a more general one listed
  first in a C# switch expression).
- `ColorGroup` switch: map the new `Kind` to a group string (reuse one, or a new one
  from step 3).
- `IsRange` (only relevant for a 2-vertical-line range fill): add the new `Kind` to
  its `is "..." or "..."` list.
- Add an `AddXxx(DateTime/double/T value)` handler wired to the new callback,
  mutating `_pending`.
- Fold the new data into `AllHorizontalLines()`/`AllVerticalLines()`/
  `AllDiagonalLines()`/`AllRangeFills()` as needed. If it should render in a colour
  *different* from the rest of its own drawing (like a reference line vs. its
  drawing's main colour), tag it explicitly here (`l with { ColorGroup = "..." }`)
  rather than through `Drawing.ColorGroup`, which colours a whole drawing uniformly.

### 5. The consuming page's markup (e.g. `ProfilerPage.razor`)
- Wire `OnXxxDrawn="AddXxx"` onto the `<DrawableOhlcvChart>` tag.
- Update the two "list of tool names" strings (the chart container's `title`, and
  the empty-state message) to mention the new tool.
- If the new data has its own per-item label, add a `@foreach` rendering it in the
  drawing card, alongside the existing H/V/D rows.

### 6. Persistence (separate concern — skip unless asked)
A "wire up the drawing only" request stops at step 5. If persistence *is* asked for,
it's its own vertical slice — worked example below (`ITRangeReferenceChartRepository`,
added on top of an already-wired-up T-Range Reference Chart):
- **New repository pair**, mirroring whichever existing one is the closest shape match
  (`ITRangeChartRepository`/`TRangeChartRepository` for a plain `[Start, End]` range):
  a public `sealed record` (bound directly into `DataTable`'s `Items`, so every column
  you want shown is a public property — computed ones like `Duration` work fine, just
  get-only), an `internal interface` with `Save*`/`List*`/`Delete*`, and the
  implementation with its own table name/SQL block (own table, not extra nullable
  columns bolted onto an existing one, if the new kind's data doesn't apply to every
  existing row).
- Register the new repository in `Program.cs` (`AddSingleton<IXxx, Xxx>`).
- Inject it into the consuming page, load its saved list in `OnInitialized`.
- `SaveSelectedDrawings()`: broaden the `Kind ==` filter to an `is "..." or "..."`
  check, then branch inside the loop — one repository call per kind, same shared
  label. Any per-kind extra data (e.g. a reference window's own start/bar count) comes
  from the fields step 4 already added to `Drawing`.
- `HandleDeleteXxx(TItem)` + `GetXxxChartBars`/`LoadXxxChartBarsAsync` (cache +
  loading-set pair, same shape as `GetChartBars`/`LoadChartBarsAsync`) to redraw a
  saved row's mini chart by re-querying ticks — nothing about the drawing's bars is
  ever stored directly (see `ITRangeChartRepository`'s doc comment for why).
- A new `<DataTable TItem="Xxx">` panel in the page's markup for the new saved list;
  give the existing one a more specific title too once there's more than one panel
  (e.g. "SAVED DRAWINGS" → "SAVED T-RANGE CHARTS") so they read as distinct sections.
- **Want the new kind's "extra" sub-range visible in the mini-chart preview, not just
  stored as numbers?** `CandlestickChart` (`Poink.Shared.Client`) takes an optional
  `ReferenceBarCount` parameter: it shades that many of `Bars`' *leading* entries
  (translucent band + dashed boundary, same colour convention as
  `DrawableOhlcvChart`'s own range fills) instead of drawing them as indistinguishable
  extra candles. To use it: extend the bar slice (e.g. `AddTRangeChart`) to include the
  extra window's bars too, and store the split index as its own field on `Drawing`
  (computed once in `FinishDrawing`, same way as `RangeBarCount`) so both the live card
  and the saved-table redraw can pass it straight through.

### 7. Build
`dotnet build Poink.Sim/Poink.Sim.csproj` — cheapest way to catch a missed spot
(commonly: forgetting to update `NewPending()`'s arg count after adding a field).

## Pitfalls hit doing this once

- **Three unsynced identity representations** (C# `DrawMode` enum / JS mode string /
  page's `Kind` string) — miss one and the tool either arms but never completes, or
  completes but gets misclassified as an existing `Kind`.
- **`Kind`'s switch is order-sensitive** — list the most specific pattern first.
- **Reject-before-push, not after** — a failed positional constraint must `return`
  before `entry.drawClicks.push(...)`, otherwise the click still counts toward the
  sequence even though nothing was drawn for it.
- **A distinct-coloured sub-element needs its own tag** at the `AllVerticalLines`/
  `AllRangeFills` call site — `Drawing.ColorGroup` alone can't express "this one
  line within an otherwise-uniform drawing is a different colour."
- **`Poink.Shared.Client`'s JS is compiled from TypeScript on every build** (see its
  `.csproj`'s `CompileTypeScript` target: `tsc --project ts/tsconfig.json`, running
  `BeforeTargets="Build"`). `CandlestickChart` and friends live in
  `Poink.Shared.Client/ts/*.ts`, not directly in `wwwroot/js/*.js` —
  `drawableOhlcvChart.js` itself (in `Poink.Sim`, no `.ts` source) is the exception.
  Edit the `.js` directly in `Poink.Shared.Client` and the next `dotnet build` silently
  overwrites it back to whatever the `.ts` source says. Check for a same-named `.ts`
  file before hand-editing any `wwwroot/js/*.js`.

## Colour groups (`groupColors` in drawableOhlcvChart.js)

| Group | CSS var (base.css) | Used by |
|---|---|---|
| `lines` | `--color-primary` | H-Line / V-Line / D-Line |
| `ranges` | `--color-secondary` | T-Range / P-Range |
| `shapes` | `--color-warning` | Rectangle |
| `charts` | `--color-accent` | T-Range Chart / T-Range Reference Chart (main range) |
| `reference` | `--color-error` | T-Range Reference Chart's reference window |
| `volume` | `--color-info` | Volume Range (drives the volume profile panel, not a chart annotation) |
| *(free)* | `--color-success` | — pick this for the next new group |

Adding a group only needs the `groupColors` entry above — no CSS file changes are
needed unless the new tool's *toolbar button* doesn't fit an existing
`.doc-tool-group-*`/`.doc-tool-btn-*` (see `DrawableOhlcvChart.razor.css`).
