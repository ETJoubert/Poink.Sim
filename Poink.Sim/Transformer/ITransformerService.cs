using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Transformer;

internal interface ITransformerService
{
    // Walks backward from beforeUtc, reading bar-aligned sub-chunks until either
    // barCount bars have been gathered, config.From is reached, or an internal step
    // cap is hit -- so a single gap (e.g. a weekend) doesn't come back as an empty
    // chunk the caller has to immediately re-request. beforeUtc is exclusive (the
    // chunk covers ticks strictly before it), matching ITickRepository.ReadTicks'
    // own [from, to) semantics.
    Task<TransformerChunk> LoadAsync(TransformerConfig config, DateTime beforeUtc, int barCount);

    // The last maxTicks raw ticks at or before beforeUtc (clamped to config.From), in
    // ascending order -- feeds TransformerPage's tick-delta chart (see
    // TickDeltaTransform), which needs individual tick prices rather than the
    // bar/histogram aggregates LoadAsync produces.
    Task<IReadOnlyList<Tick>> LoadRecentTicksAsync(TransformerConfig config, DateTime beforeUtc, int maxTicks);

    // Every raw tick in [fromUtc, toUtc), uncapped, in ascending order -- feeds
    // TransformerPage's Subchart tab, which (unlike LoadRecentTicksAsync's callers)
    // needs the full selected Volume Range rather than a bounded "last N ticks" window.
    Task<IReadOnlyList<Tick>> LoadTicksInRangeAsync(TransformerConfig config, DateTime fromUtc, DateTime toUtc);
}
