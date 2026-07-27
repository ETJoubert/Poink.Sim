# Poink.Sim Docs

Per-area design docs for Poink.Sim. Start at [../README.md](../README.md) for
the whole-project abstraction; come here for depth on one of its four areas.

| Doc | Area |
|---|---|
| [Simulation.md](Simulation.md) | Interactive tick-by-tick replay against a live Brain + Trader |
| [Training.md](Training.md) | Headless reinforcement-learning training of a trainable Brain |
| [../Brain/README.md](../Brain/README.md) | The Brain abstraction itself (heuristic + trained, composition model) |
| [Profiler.md](Profiler.md) | Batch (non-playback) chart + volume/time-profile viewer |

See also [../GLOSSARY.md](../GLOSSARY.md) — the flat term-by-term reference
index shared across all four areas.

## Documentation standards

These apply to every doc in the table above (including `../Brain/README.md`
and this file's own siblings), not to `../GLOSSARY.md` (a flat reference
index, not a narrative doc) or `../TODO/*.md` (work planning, which already
has its own Status-line convention — see `../TODO/README.md`).

**Current-state only.** A doc's body describes what *is*, not what *was*.
No "used to be," no build-log checklists of done/pending work mixed into the
design prose — that kind of history rots the moment the next change lands
and nobody remembers to hunt down every sentence it invalidated. Prior
design context that's still decision-relevant (a rejected alternative and
*why*) is fine; a play-by-play of how the feature got built is not. Planned/
in-flight work belongs in `../TODO/*.md`.

**Every doc carries a version header,** directly under the H1:

```markdown
# Title

> Doc version: **v1.2** — see "Changelog" at the bottom for what changed.
```

**Every doc ends with a Changelog section,** most-recent-first:

```markdown
## Changelog

- **v1.2** — 2026-07-27 — Added multi-trader sessions (`TraderCount`,
  manual-mode trading) to the playback design.
- **v1.1** — 2026-07-22 — Documented the uncapped Max playback mode.
- **v1.0** — 2026-07-27 — Split out of the monolithic root README.
```

This is the "version control" for the docs themselves: git already tracks
*every* edit, but not every edit is worth a reader's attention. The
Changelog is a curated trail of the changes that actually moved the design
— what a returning reader (or an agent re-orienting on this project) should
skim to catch up, without diffing prose against git blame.

**Version numbers** are `vMAJOR.MINOR`, not strict semver:
- **MAJOR** bumps on a structural rewrite — the doc's shape changes, not
  just its content (e.g. this split from one root README into per-area docs).
- **MINOR** bumps whenever the *current-state* content changes to track a
  real behavior change in the code the doc describes.
- A change that doesn't affect what a reader learns from the doc (typo fix,
  rewording) doesn't need a version bump or a Changelog line.

**One doc, one area.** Keep `Simulation.md`/`Training.md`/`Profiler.md`
scoped to their own area; shared concepts (`Tick`, `ITickRepository`,
`HlocAvdtBar`) stay in `GLOSSARY.md` rather than being redefined per doc.

## Changelog

- **v1.0** — 2026-07-27 — Initial standards doc, written alongside the
  Simulation/Training/Profiler doc split.
