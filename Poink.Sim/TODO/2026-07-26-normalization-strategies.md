# Investigate normalization strategies for observation encoders

Status: Pending Approval

## Update (2026-07-26)

`CapitalStateEncoder` was renamed to `TraderStateEncoder` and its
ratios-over-equity approach was replaced outright, on explicit request: every
field (position direction, current price, cash, realized/unrealized PnL,
equity, margin, margin level, position count) is now fed to the network
exactly as `Trader`/`CapitalState` report it (`RawValueNormalizer`/
`RawPriceNormalizer` passthrough), not scaled against equity. This supersedes
this file's "Non-goals" note below, which assumed that approach wasn't in
question. Separately, `VolumeProfileRawEncoder`/`TimeProfileRawEncoder` were
added *alongside* (not replacing) `VolumeProfileEncoder`/`TimeProfileEncoder`,
giving the same histogram/summary an unnormalized option (exact tick
counts/seconds and exact price levels) a brain can pick instead of the
fraction/offset convention this file investigates below. The scope below
(`BarWindowEncoder`'s price window, and the fraction/offset convention as
still used by the non-raw Volume/Time Profile encoders) is otherwise
unchanged and still pending.

## Why

Today every `IObservationEncoder` (`BarWindowEncoder`, `VolumeProfileEncoder`)
normalizes with one convention, chosen ad hoc when each was extracted rather
than evaluated against alternatives:

- **`BarWindowEncoder`**: simple percent-change-from-current-price,
  `(price - currentPrice) / currentPrice`, zero-padded when history is short.
- **`VolumeProfileEncoder`** / `VolumeProfileAnalysis`: same relative-offset
  formula for POC/value-area scalars, plus fraction-of-total-volume for the
  20-bucket histogram. Explicitly documented as matching `BarWindowEncoder`'s
  convention "so a network sees the same kind of bounded, scale-free number
  regardless of the instrument's absolute price level." (`VolumeProfileRawEncoder`
  now offers an unnormalized alternative -- see Update above.)

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

- No code changes yet to `BarWindowEncoder`/the non-raw Volume/Time Profile
  encoders -- this remains investigation/decision only for those. If it leads
  to a concrete change, split that into its own plan file per the TODO
  convention. (`TraderStateEncoder`'s equity-ratio approach and a raw
  Volume/Time Profile option were already decided/implemented outside this
  investigation -- see Update above.)

## Verification

- This file updated with findings and a decision (keep current convention /
  adopt an alternative), or superseded by a new plan file if a change is
  approved.
