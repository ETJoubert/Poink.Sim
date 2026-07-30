# Profiler — batch chart + volume/time profile viewer

> Doc version: **v1.0** — see "Changelog" at the bottom for what changed.
> See [README.md](README.md) for documentation standards.

## What it is

The batch (non-playback) counterpart to Simulation: pick a symbol and date
range, then look at its candlestick chart and Volume/Time Profile — no
Brain, Trader, statistics, or play/pause/step/speed involved. Where
Simulation is stateful tick-by-tick playback driving a live Brain/Trader,
Profiler only ever needs "bars ending at some point, going back as far as
the chart has actually been panned."

## Backward-chunked loading

`ProfilerService.LoadAsync` doesn't aggregate the whole selected range up
front. It pulls one bar-aligned chunk at a time, **newest-first**: the
initial **LOAD** fetches only a recent slice (`BarsPerFetch` bars) for a
fast first paint, and `ProfilerPage` requests the next older chunk on
demand as the user pans the chart back toward its oldest loaded bar (wired
through `DrawableOhlcvChart`'s `OnNearStart` → `LoadMore`).

Each chunk streams straight from Postgres through the same
`SimulationBarAggregator` Simulation uses, one tick at a time, keeping only
the resulting bars/histograms in memory rather than the chunk's raw ticks
(`ITickRepository.ReadTicks` is already a lazy, unbuffered reader).
`LoadAsync` walks backward in bar-aligned sub-steps (snapped to the same
bar-boundary grid the aggregator uses) until either the requested bar count
is gathered, `config.From` is reached, or a safety cap (`MaxSteps`) is hit —
so one sparse stretch (e.g. a weekend gap) doesn't come back as an empty
chunk the caller has to immediately re-request.

## Chart + profile panels

- **Main chart** (`DrawableOhlcvChart`) — candlestick + volume, with
  click-to-draw annotations and lazy pan-triggered backfill (see above).
- **Volume Profile** (`TickVolumeProfileChart`) / **Time Profile**
  (`TickTimeProfileChart`) — tick-count-by-price and dwell-time-by-price
  histograms over the loaded window, each with its own Point of
  Control/Value Area, rendered as pure Razor/CSS panels next to the main
  chart. Both are also projected onto the main chart as POC/VAH/VAL lines.
- **VWAP** — a native lightweight-charts line series over the loaded bars.

See [../GLOSSARY.md](../GLOSSARY.md) for the Volume Profile / Time Profile /
POC / Value Area / VWAP definitions — Profiler and Simulation share the
exact same computation (`VolumeProfileStats`/`TimeProfileStats`).

## Drawing tools

`DrawableOhlcvChart`'s toolbar supports click-to-draw annotations, grouped
in `ProfilerPage` into one `Drawing` per placed shape:

| Tool | Shape | Persisted? |
|---|---|---|
| H-Line / V-Line / D-Line | One horizontal/vertical/diagonal line | No — session-only |
| P-Range / T-Range | Two horizontal/vertical lines marking a price or time range | No — session-only |
| Rectangle | Two horizontal + two vertical lines | No — session-only |
| T-Range Chart | A T-Range plus a snapshot of the bars between its two lines, shown as a mini candlestick card | Yes — `ITRangeChartRepository` |
| T-Range Reference Chart | A T-Range Chart plus a third click marking a separate "reference window" for comparison | Yes — `ITRangeReferenceChartRepository` |

A saved T-Range (Reference) Chart persists only the *range it marks* —
symbol, start/end, bar size, bar count (plus the reference window's own
start/end/bar count for a Reference Chart) — not a bar/line snapshot. It's
redrawn later by re-querying that symbol's ticks for the same window via
`ProfilerService.LoadAsync`, the same aggregation the live chart used when
the range was first drawn. Both repositories write into the same Postgres
database as `IModelRepository` (`Poink.Sim`, not the tick database), since
neither is market data. `SavedTRangeCharts`/`SavedTRangeReferenceCharts`
panels (below the chart, next to the Drawings panel) list, redraw, and
delete saved entries via a shared `DataTable`.

## Files

| Folder | File | Role |
|---|---|---|
| `Profiler/` | `ProfilerService.cs` | Backward-chunked tick → bar/histogram loader (scoped, no shared "current run" state) |
| `Models/` | `ProfilerModels.cs` | `ProfilerConfig`/`ProfilerChunk` read-model |
| `Models/` | `VolumeProfileAnalysis.cs`, `TimeProfileAnalysis.cs` | Shared Volume/Time Profile computation, also used by Training's `VolumeProfileEncoder`/`TimeProfileEncoder` |
| `Repository/` | `ITRangeChartRepository.cs`, `TRangeChartRepository.cs` | Saved T-Range Chart storage |
| `Repository/` | `ITRangeReferenceChartRepository.cs`, `TRangeReferenceChartRepository.cs` | Saved T-Range Reference Chart storage |
| `Components/Pages/Profiler/` | `ProfilerPage.razor(.cs/.css)` | The `/profiler` page: setup form, chart, profile panels, drawing tools, saved-charts tables |
| `Components/Charts/` | `DrawableOhlcvChart/`, `TickVolumeProfileChart.razor.cs`, `TickTimeProfileChart.razor.cs` | Shared chart components (also used by Training's episode replay) |

## Using it

Browse to `/profiler`. Pick a symbol, a date range, and a bar size, then
**Load** for a fast first paint of the most recent bars — pan the chart
left to backfill older history on demand. Use the toolbar to place lines/
ranges/rectangles, or T-Range (Reference) Chart to snapshot a range as a
saved, reloadable mini-chart. The Drawings/Saved T-Range Charts/Saved
T-Range Reference Charts panels below the chart list what's on it.

## Changelog

- **v1.0** — 2026-07-27 — Initial doc, split out of the Simulation area
  (Profiler was previously undocumented in the root README) into its own
  file per the four-area split.
