// ── Shared "loaded tick data" cache ──────────────────────────────────────
//  Singleton so SimulationPage/TrainPage share one loaded data set instead of
//  each re-reading Postgres on its own. The explicit "LOAD DATA" button on both
//  setup screens calls TryLoad here; SimulationEngine/RlTrainingService then
//  consume the already-materialized ticks (see their TryLoad/TryStartTraining
//  signatures) instead of reading Postgres themselves.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Poink.Sim.Repository;

internal sealed class TickDataStore
{
    private readonly Lock _lock = new();
    private readonly ITickRepository _tickRepository;

    private string? _table;
    private DateTime _from;
    private DateTime _to;
    private List<Tick>? _ticks;
    private bool _isLoading;
    private string? _error;

    public TickDataStore(ITickRepository tickRepository) => _tickRepository = tickRepository;

    public event Action? OnChanged;

    public bool IsLoading { get { lock (_lock) return _isLoading; } }
    public string? Error { get { lock (_lock) return _error; } }
    public int LoadedCount { get { lock (_lock) return _ticks?.Count ?? 0; } }

    // True only when the cached ticks match exactly the requested table+range --
    // changing either on the setup form invalidates readiness immediately, so a
    // stale load can never silently back a run.
    public bool IsLoadedFor(string table, DateTime from, DateTime to)
    {
        lock (_lock) return _ticks is not null && _table == table && _from == from && _to == to;
    }

    public List<Tick>? GetLoaded(string table, DateTime from, DateTime to)
    {
        lock (_lock) return _ticks is not null && _table == table && _from == from && _to == to ? _ticks : null;
    }

    public bool TryLoad(string table, DateTime from, DateTime to)
    {
        lock (_lock)
        {
            if (_isLoading) return false;
            _isLoading = true;
            _error = null;
        }
        OnChanged?.Invoke();

        _ = Task.Run(() =>
        {
            try
            {
                // Real feed data (unlike DummyTickGenerator's synthetic walk) can contain
                // bad rows -- zero/negative/non-finite bid or ask from a gap or a writer-side
                // bug -- that would otherwise propagate as NaN/Infinity all the way through
                // Trader's mark-to-market and a policy brain's features/logits.
                var ticks = _tickRepository.ReadTicks(table, from, to)
                    .Where(t => t.Bid > 0 && t.Ask > 0 && double.IsFinite(t.Bid) && double.IsFinite(t.Ask))
                    .ToList();
                if (ticks.Count == 0)
                    throw new InvalidOperationException("no ticks in the selected range");

                lock (_lock)
                {
                    _table = table;
                    _from = from;
                    _to = to;
                    _ticks = ticks;
                    _isLoading = false;
                }
            }
            catch (Exception ex)
            {
                lock (_lock) { _isLoading = false; _error = ex.Message; _ticks = null; _table = null; }
            }
            OnChanged?.Invoke();
        });

        return true;
    }
}
