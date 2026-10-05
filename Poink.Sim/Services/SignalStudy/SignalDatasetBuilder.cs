using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Charts;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.SignalStudy;

/// <summary>
/// Builds signal datasets with the same market feed and rule-engine rows the robot uses, sampling the first tick of
/// every second. TxFold (loaded asynchronously) and position rows (which need a trade) are not part of a dataset.
/// </summary>
internal sealed class SignalDatasetBuilder : ISignalDatasetBuilder
{
	private const int BatchSize = 20_000;
	private const int CancellationCheckTicks = 4096;
	private const int WarmupProgressTicks = 200_000;
	private const int MaxSignalRows = 64;

	private static readonly HashSet<RuleSignalGroup> ExcludedGroups = new() { RuleSignalGroup.TxFold, RuleSignalGroup.Position };

	private readonly ITickRepository _tickRepository;
	private readonly ISignalDatasetRepository _datasetRepository;
	private readonly ILogger<SignalDatasetBuilder> _logger;
	private readonly object _lock = new();
	private CancellationTokenSource? _cancellation;

	public SignalDatasetBuilder(
		ITickRepository tickRepository,
		ISignalDatasetRepository datasetRepository,
		ILogger<SignalDatasetBuilder> logger
	)
	{
		_tickRepository = tickRepository;
		_datasetRepository = datasetRepository;
		_logger = logger;
	}

	public bool IsBuilding { get; private set; }
	public SignalDatasetBuildProgress? Progress { get; private set; }
	public string? LastError { get; private set; }
	public bool WasCancelled { get; private set; }

	public event Action? OnChanged;

	public bool TryStart(SignalDatasetBuildRequest request)
	{
		CancellationToken token;
		lock (_lock)
		{
			if (IsBuilding)
				return false;
			IsBuilding = true;
			LastError = null;
			WasCancelled = false;
			Progress = new SignalDatasetBuildProgress(request.Name, request.FromUtc, request.ToUtc, null, 0, true);
			_cancellation = new CancellationTokenSource();
			token = _cancellation.Token;
		}
		OnChanged?.Invoke();
		_ = Task.Run(() => Run(request, token));
		return true;
	}

	public void Cancel()
	{
		lock (_lock)
		{
			_cancellation?.Cancel();
		}
	}

	private void Run(SignalDatasetBuildRequest request, CancellationToken token)
	{
		var stopwatch = Stopwatch.StartNew();
		var catalog = new SignalCatalog(MaxSignalRows);
		long datasetId = 0;
		long pointCount = 0;
		var status = SignalDatasetStatus.Failed;
		_logger.LogInformation(
			"Signal dataset build started: name={Name}, table={Table}, from={From}, to={To}, pipSize={PipSize}, profile={@Profile}",
			request.Name,
			request.Table,
			request.FromUtc,
			request.ToUtc,
			request.PipSize,
			request.Profile
		);
		try
		{
			datasetId = _datasetRepository.CreateDataset(request);
			pointCount = BuildPoints(request, datasetId, catalog, token);
			status = SignalDatasetStatus.Complete;
		}
		catch (OperationCanceledException)
		{
			status = SignalDatasetStatus.Cancelled;
			WasCancelled = true;
		}
		catch (Exception ex)
		{
			LastError = ex.Message;
			_logger.LogError(ex, "Signal dataset build failed for {Table}", request.Table);
		}

		if (datasetId > 0)
			TryFinish(datasetId, status, catalog, pointCount);
		if (catalog.Overflowed)
			_logger.LogWarning("Signal dataset {Id} had more than {Max} signal rows; the extra rows were not stored", datasetId, MaxSignalRows);
		_logger.LogInformation(
			"Signal dataset build ended: id={Id}, status={Status}, points={PointCount}, signals={SignalCount}, elapsed={Elapsed}",
			datasetId,
			status,
			pointCount,
			catalog.Entries.Count,
			stopwatch.Elapsed
		);

		lock (_lock)
		{
			IsBuilding = false;
			_cancellation?.Dispose();
			_cancellation = null;
		}
		OnChanged?.Invoke();
	}

	private void TryFinish(long datasetId, SignalDatasetStatus status, SignalCatalog catalog, long pointCount)
	{
		try
		{
			_datasetRepository.FinishDataset(datasetId, status, catalog.Entries, pointCount);
		}
		catch (Exception ex)
		{
			LastError ??= ex.Message;
			_logger.LogError(ex, "Could not record how signal dataset {Id} ended", datasetId);
		}
	}

	// The feed runs on every tick, including a warm-up of a full bar buffer before From, so the first sampled second
	// already sees the same bar history a long-running simulation would.
	private long BuildPoints(SignalDatasetBuildRequest request, long datasetId, SignalCatalog catalog, CancellationToken token)
	{
		var profile = request.Profile;
		var settings = profile.Signals;
		var barSize = TimeSpan.FromSeconds(Math.Max(1, profile.BarSizeSeconds));
		var loadFrom = request.FromUtc - barSize * SimulationMarketFeed.MaxRecentBars;
		var rules = new RobotRules(new HashSet<string>(), profile.XGridPercent, ExcludedGroups, profile.InTolerancePips, null);

		var feed = new SimulationMarketFeed();
		feed.Reset(true, barSize, request.PipSize);
		var batch = new List<SignalPoint>(BatchSize);
		var bars = new SampledBarData(profile, request.PipSize);
		long pointCount = 0;
		long ticksRead = 0;
		long lastSecond = long.MinValue;

		using var reader = new PrefetchingTickReader(() => _tickRepository.ReadTicks(request.Table, loadFrom, request.ToUtc).GetEnumerator());
		while (reader.MoveNext())
		{
			var tick = reader.Current;
			if (++ticksRead % CancellationCheckTicks == 0)
				token.ThrowIfCancellationRequested();

			feed.Feed(tick);
			if (tick.Timestamp < request.FromUtc)
			{
				if (ticksRead % WarmupProgressTicks == 0)
					ReportProgress(request, tick.Timestamp, pointCount, isWarmingUp: true);
				continue;
			}

			long second = tick.Timestamp.Ticks / TimeSpan.TicksPerSecond;
			if (second == lastSecond || bars.Refresh(feed) is not { } barData)
				continue;
			lastSecond = second;

			var market = new RuleSignalMarket(
				barData.Bars,
				barData.Window,
				tick.Bid,
				tick.Ask,
				feed.TypicalSpread,
				tick.Timestamp,
				barSize,
				settings
			);
			var rows = new RobotRuleContext(barData, (tick.Bid + tick.Ask) / 2.0, Array.Empty<TxFoldBar>(), market).MarketRowsFor(rules);
			var (above, inBand, below) = catalog.Encode(rows);
			batch.Add(new SignalPoint(tick.Timestamp, tick.Bid, tick.Ask, bars.Atr, above, inBand, below));

			if (batch.Count < BatchSize)
				continue;
			_datasetRepository.AppendPoints(datasetId, batch);
			pointCount += batch.Count;
			batch.Clear();
			ReportProgress(request, tick.Timestamp, pointCount, isWarmingUp: false);
		}

		token.ThrowIfCancellationRequested();
		_datasetRepository.AppendPoints(datasetId, batch);
		pointCount += batch.Count;
		ReportProgress(request, request.ToUtc, pointCount, isWarmingUp: false);
		return pointCount;
	}

	private void ReportProgress(SignalDatasetBuildRequest request, DateTime current, long pointCount, bool isWarmingUp)
	{
		Progress = new SignalDatasetBuildProgress(request.Name, request.FromUtc, request.ToUtc, current, pointCount, isWarmingUp);
		OnChanged?.Invoke();
	}

	/// <summary>The bar-derived inputs (window, grid, ATR), rebuilt only when a bar completes.</summary>
	private sealed class SampledBarData
	{
		private readonly SignalProfile _profile;
		private readonly double _pipSize;
		private (int Count, DateTime LastOpen)? _key;
		private RobotBarData? _barData;

		public SampledBarData(SignalProfile profile, double pipSize)
		{
			_profile = profile;
			_pipSize = pipSize;
		}

		public double? Atr { get; private set; }

		public RobotBarData? Refresh(SimulationMarketFeed feed)
		{
			if (feed.RecentBars.Count == 0)
				return null;
			var key = (feed.RecentBars.Count, feed.RecentBars[^1].OpenTime);
			if (_key == key)
				return _barData;

			_key = key;
			_barData = RobotBarData.Create(
				feed.RecentBars,
				feed.RecentHistograms,
				_pipSize,
				_profile.BarWindow,
				_profile.BarOffset,
				_profile.XGridPips,
				_profile.XGridCols,
				includeGrid: true
			);
			var atrSeries = _barData is null ? null : MarketRegimeHelper.AtrSeries(_barData.Bars, _profile.Signals.AtrPeriod);
			Atr = atrSeries is { Count: > 0 } ? atrSeries[^1] : null;
			return _barData;
		}
	}

	/// <summary>Gives each signal row a fixed bit the first time it appears, and packs a sample's rows into Above/In/Below masks.</summary>
	private sealed class SignalCatalog
	{
		private readonly int _maxRows;
		private readonly Dictionary<string, int> _indexByInKey = new();
		private readonly List<SignalCatalogEntry> _entries = new();

		public SignalCatalog(int maxRows) => _maxRows = maxRows;

		public IReadOnlyList<SignalCatalogEntry> Entries => _entries;
		public bool Overflowed { get; private set; }

		public (long Above, long In, long Below) Encode(IReadOnlyList<RuleSignalRow> rows)
		{
			long above = 0,
				inBand = 0,
				below = 0;
			foreach (var row in rows)
			{
				if (IndexOf(row) is not { } index)
					continue;
				long bit = 1L << index;
				if (row.Above)
					above |= bit;
				if (row.In)
					inBand |= bit;
				if (row.Below)
					below |= bit;
			}
			return (above, inBand, below);
		}

		private int? IndexOf(RuleSignalRow row)
		{
			if (_indexByInKey.TryGetValue(row.InKey, out var index))
				return index;
			if (_entries.Count >= _maxRows)
			{
				Overflowed = true;
				return null;
			}

			index = _entries.Count;
			_indexByInKey[row.InKey] = index;
			_entries.Add(new SignalCatalogEntry(index, row.RowLabel, row.Group, row.AboveKey, row.InKey, row.BelowKey, row.Description));
			return index;
		}
	}
}
