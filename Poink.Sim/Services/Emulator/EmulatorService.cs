using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Emulator.Bots;
using Poink.Sim.Emulator.Broker;

namespace Poink.Sim.Emulator;

/// <summary>
/// Process-wide emulated brokerage for one synthetic asset. Each step, bots in random order each get a chance to act on
/// what they alone can see; the broker fills their orders, which moves the price, then stops out under-margined bots.
/// Each timer tick runs <see cref="EmulatorSettings.SpeedMultiplier"/> steps, cut short when they would overrun the tick
/// so the page keeps its frame rate, and records one history point. The snapshot is replaced whole, so readers never see
/// a half-step and never wait on a running batch.
/// </summary>
public sealed class EmulatorService : IEmulatorService, IDisposable
{
	private const double TickBudgetFraction = 0.8;
	private const double StepsPerSecondSmoothing = 0.2;

	private readonly IEmulatorBotStrategy _strategy;
	private readonly ILogger<EmulatorService> _logger;
	private readonly TimeProvider _timeProvider;
	private readonly Random _random;
	private readonly object _lock = new();
	private readonly Queue<EmulatorPricePoint> _history = new();

	private EmulatorBroker _broker;
	private volatile EmulatorSnapshot _snapshot;
	private volatile EmulatorSettings _settings;
	private volatile CancellationTokenSource? _loopCancellation;
	private double _stepsPerSecond;

	public EmulatorService(IEmulatorBotStrategy strategy, ILogger<EmulatorService> logger)
		: this(strategy, logger, new EmulatorSettings(), TimeProvider.System, new Random()) { }

	internal EmulatorService(
		IEmulatorBotStrategy strategy,
		ILogger<EmulatorService> logger,
		EmulatorSettings settings,
		TimeProvider timeProvider,
		Random random
	)
	{
		_strategy = strategy;
		_logger = logger;
		_timeProvider = timeProvider;
		_random = random;
		_settings = settings;
		(_broker, _snapshot) = OpenWarmedUpMarket();
	}

	public EmulatorSettings Settings => _settings;

	public EmulatorSnapshot Snapshot => _snapshot;

	public bool IsRunning => _loopCancellation is not null;

	public double StepsPerSecond => IsRunning ? Volatile.Read(ref _stepsPerSecond) : 0;

	public event Action? Changed;

	public void Start()
	{
		CancellationTokenSource cancellation;
		lock (_lock)
		{
			if (_loopCancellation is not null)
			{
				return;
			}
			cancellation = _loopCancellation = new CancellationTokenSource();
		}

		_ = Task.Run(() => RunLoopAsync(cancellation.Token));
		_logger.LogInformation(
			"Emulator started for {Symbol}, {Speed}× speed every {TickInterval} with {Settings}",
			Settings.Symbol,
			Settings.SpeedMultiplier,
			Settings.TickInterval,
			Settings
		);
		Changed?.Invoke();
	}

	public void Stop()
	{
		if (!TryCancelLoop())
		{
			return;
		}

		_logger.LogInformation("Emulator stopped for {Symbol} at step {Step}", Settings.Symbol, Snapshot.Step);
		Changed?.Invoke();
	}

	public void Step() => Advance(1);

	public void Reset()
	{
		lock (_lock)
		{
			(_broker, _snapshot) = OpenWarmedUpMarket();
		}

		_logger.LogInformation(
			"Emulator reset for {Symbol} with populations {Populations} and {Settings}",
			Settings.Symbol,
			DescribePopulations(),
			Settings
		);
		Changed?.Invoke();
	}

	public void SetSpeed(int speedMultiplier)
	{
		if (!EmulatorSettings.SpeedMultipliers.Contains(speedMultiplier))
		{
			throw new ArgumentOutOfRangeException(nameof(speedMultiplier), speedMultiplier, "Speed must be one of the playback speeds.");
		}

		lock (_lock)
		{
			_settings = _settings with { SpeedMultiplier = speedMultiplier };
		}

		_logger.LogInformation("Emulator speed set to {Speed}× for {Symbol}", speedMultiplier, Settings.Symbol);
		Changed?.Invoke();
	}

	public void ApplySettings(EmulatorSettings settings)
	{
		bool reopened;
		lock (_lock)
		{
			reopened = Settings.RequiresReopen(settings);
			_settings = settings;
			if (reopened)
			{
				(_broker, _snapshot) = OpenWarmedUpMarket();
			}
			else
			{
				_broker.UpdateTerms(settings);
				_snapshot = BuildSnapshot(_broker, _snapshot.Step);
			}
		}

		_logger.LogInformation(
			"Emulator settings applied for {Symbol} ({Mode}): populations {Populations}, {Settings}",
			Settings.Symbol,
			reopened ? "market reopened" : "live",
			DescribePopulations(),
			Settings
		);
		Changed?.Invoke();
	}

	public void Dispose() => TryCancelLoop();

	/// <summary>
	/// Runs up to <paramref name="steps"/> market steps as one batch, stopping early once <paramref name="budget"/> is spent,
	/// then records one history point and one snapshot. Returns the steps run.
	/// </summary>
	internal int Advance(int steps, TimeSpan? budget = null)
	{
		var started = _timeProvider.GetTimestamp();
		var done = 0;
		lock (_lock)
		{
			while (done < steps)
			{
				AdvanceMarket(_broker);
				done++;
				if (budget is { } limit && _timeProvider.GetElapsedTime(started) >= limit)
				{
					break;
				}
			}

			AppendHistory(new EmulatorPricePoint(_timeProvider.GetUtcNow().UtcDateTime, _broker.LastPrice));
			_snapshot = BuildSnapshot(_broker, _snapshot.Step + done);
		}

		Changed?.Invoke();
		return done;
	}

	private string DescribePopulations() =>
		string.Join(", ", Settings.Populations.Select(p => $"{p.Name}: {p.BotCount} bots × {p.StartingPoints:N0} points"));

	private async Task RunLoopAsync(CancellationToken token)
	{
		try
		{
			var budget = Settings.TickInterval * TickBudgetFraction;
			using var timer = new PeriodicTimer(Settings.TickInterval, _timeProvider);
			var lastTick = _timeProvider.GetTimestamp();
			Volatile.Write(ref _stepsPerSecond, 0);
			while (await timer.WaitForNextTickAsync(token))
			{
				var done = Advance(Math.Max(1, Settings.SpeedMultiplier), budget);
				var now = _timeProvider.GetTimestamp();
				var rate = done / Math.Max(_timeProvider.GetElapsedTime(lastTick, now).TotalSeconds, 1e-3);
				lastTick = now;
				var smoothed = Volatile.Read(ref _stepsPerSecond);
				Volatile.Write(ref _stepsPerSecond, smoothed == 0 ? rate : smoothed + (rate - smoothed) * StepsPerSecondSmoothing);
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			_logger.LogError(ex, "Emulator step loop failed for {Symbol}; stopping", Settings.Symbol);
			TryCancelLoop();
			Changed?.Invoke();
		}
	}

	private bool TryCancelLoop()
	{
		CancellationTokenSource? cancellation;
		lock (_lock)
		{
			cancellation = _loopCancellation;
			_loopCancellation = null;
		}

		if (cancellation is null)
		{
			return false;
		}

		cancellation.Cancel();
		cancellation.Dispose();
		return true;
	}

	private (EmulatorBroker Broker, EmulatorSnapshot Snapshot) OpenWarmedUpMarket()
	{
		_history.Clear();
		var broker = new EmulatorBroker(Settings);
		var now = _timeProvider.GetUtcNow().UtcDateTime;
		AppendHistory(new EmulatorPricePoint(now - Settings.TickInterval * Math.Max(0, Settings.WarmupSteps), broker.LastPrice));
		for (var step = Settings.WarmupSteps - 1; step >= 0; step--)
		{
			AdvanceMarket(broker);
			AppendHistory(new EmulatorPricePoint(now - Settings.TickInterval * step, broker.LastPrice));
		}

		return (broker, BuildSnapshot(broker, 0));
	}

	private void AdvanceMarket(EmulatorBroker broker)
	{
		broker.BeginStep();
		var turnOrder = broker.Accounts.ToArray();
		_random.Shuffle(turnOrder);
		foreach (var bot in turnOrder)
		{
			if (!bot.IsBust && _random.NextDouble() < Settings.ActivityChance)
			{
				broker.Execute(bot, _strategy.Decide(broker.ViewFor(bot), Settings));
			}
		}

		broker.RunStopOuts();
	}

	private EmulatorSnapshot BuildSnapshot(EmulatorBroker broker, long step) =>
		new(
			Settings.Symbol,
			broker.LastPrice,
			broker.Book(),
			broker.RestingPopulation(),
			broker.PopulationSummaries(),
			broker.BotSummaries(),
			broker.Summary(),
			_history.ToArray(),
			step
		);

	private void AppendHistory(EmulatorPricePoint point)
	{
		_history.Enqueue(point);
		while (_history.Count > Math.Max(1, Settings.HistoryCapacity))
		{
			_history.Dequeue();
		}
	}
}
