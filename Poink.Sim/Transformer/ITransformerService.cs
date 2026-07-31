using System;
using System.Threading.Tasks;
using Poink.Sim.Models;

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
}
