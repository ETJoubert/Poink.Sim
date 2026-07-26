# Investigate normalization strategies for observation encoders

Status: Pending Approval

## Why

Today every `IObservationEncoder` (`BarWindowEncoder`, `VolumeProfileEncoder`,
`CapitalStateEncoder`) normalizes with one convention, chosen ad hoc when each
was extracted rather than evaluated against alternatives:

- **`BarWindowEncoder`**: simple percent-change-from-current-price,
  `(price - currentPrice) / currentPrice`, zero-padded when history is short.
- **`VolumeProfileEncoder`** / `VolumeProfileAnalysis`: same relative-offset
  formula for POC/value-area scalars, plus fraction-of-total-volume for the
  20-bucket histogram. Explicitly documented as matching `BarWindowEncoder`'s
  convention "so a network sees the same kind of bounded, scale-free number
  regardless of the instrument's absolute price level."
- **`CapitalStateEncoder`**: ratios over equity (unrealized/realized PnL,
  cash), already scale-free by construction.

This is a reasonable default but hasn't been checked against alternatives that
are standard in financial ML. Worth a deliberate investigation before more
encoders get added on top of the same convention by default.

## Scope

Investigate (no implementation yet -- this file is the record of the
investigation and its outcome):

- **Log-returns vs simple percent-change** for `BarWindowEncoder`'s price
  window -- log-returns are additive across time and better-behaved for large
  moves; check whether the difference matters at this window size (20 bars)
  and instrument set.
- **Volatility-adjusted scaling** (e.g. ATR-relative or rolling-std-relative
  offsets) instead of raw `/currentPrice` for both the price window and the
  volume-profile POC/value-area offsets -- a fixed-% offset means the same
  encoded value represents very different real moves in calm vs volatile
  regimes; check whether this confuses the network across regime changes.
- **Rolling z-score normalization** (mean/std over the lookback window) as an
  alternative to relative-to-current-price for the price window.
- **Outlier handling** -- whether raw ticks/bad prints/gaps can produce
  outsized relative-offset values today, and whether clipping/clamping bounds
  are needed before they reach the network.
- **Volume histogram bucket-width consistency** -- `BuildRelativeHistogram`
  re-bins to a fixed bucket *count* but bucket *width* still varies with the
  min/max price range actually seen in the lookback window; check whether
  this makes histograms harder to compare episode-to-episode and whether a
  fixed-width (rather than fixed-count) binning would be more consistent.
- Whether any of the above are worth the added complexity given this is a
  solo-dev project with a fixed, known instrument set today, or whether the
  current convention should just be documented as the deliberate choice and
  left alone.

## Non-goals

- No code changes yet -- this is investigation/decision only. If it leads to
  a concrete change, split that into its own plan file per the TODO
  convention.
- Not re-litigating `CapitalStateEncoder`'s equity-ratio approach -- that's
  already scale-free and not in question here.

## Verification

- This file updated with findings and a decision (keep current convention /
  adopt an alternative), or superseded by a new plan file if a change is
  approved.
