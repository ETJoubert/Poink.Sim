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
/// The snapshot is replaced whole so readers never see a half-step.
/// </summary>
public sealed class EmulatorService : IEmulatorService, IDisposable
{
	private readonly IEmulatorBotStrategy _strategy;
	private readonly ILogger<EmulatorService> _logger;
	private readonly TimeProvider _timeProvider;
	private readonly Random _random;
	private readonly object _lock = new();
	private readonly Queue<EmulatorPricePoint> _history = new();

	private EmulatorBroker _broker;
	private EmulatorSnapshot _snapshot;
	private CancellationTokenSource? _loopCancellation;

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
		Settings = settings;
		(_broker, _snapshot) = OpenWarmedUpMarket();
	}

	public EmulatorSettings Settings { get; private set; }

	public EmulatorSnapshot Snapshot
	{
		get
		{
			lock (_lock)
			{
				return _snapshot;
			}
		}
	}

	public bool IsRunning
	{
		get
		{
			lock (_lock)
			{
				return _loopCancellation is not null;
			}
		}
	}

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
			"Emulator started for {Symbol}, stepping every {StepInterval} with {Settings}",
			Settings.Symbol,
			Settings.StepInterval,
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

	public void Step()
	{
		lock (_lock)
		{
			AdvanceMarket(_broker, _timeProvider.GetUtcNow().UtcDateTime);
			_snapshot = BuildSnapshot(_broker, _snapshot.Step + 1);
		}

		Changed?.Invoke();
	}

	public void Reset()
	{
		lock (_lock)
		{
			(_broker, _snapshot) = OpenWarmedUpMarket();
		}

		_logger.LogInformation(
			"Emulator reset for {Symbol} with populations {Populations} and {Settings}",
			Settings.Symbol,
			string.Join(", ", Settings.Populations.Select(p => $"{p.Name}: {p.BotCount} bots Ã— {p.StartingPoints:N0} points")),
			Settings
		);
		Changed?.Invoke();
	}

	public void ApplySettings(EmulatorSettings settings)
	{
		lock (_lock)
		{
			Settings = settings;
			(_broker, _snapshot) = OpenWarmedUpMarket();
		}

		_logger.LogInformation(
			"Emulator settings applied for {Symbol}: populations {Populations}, {Settings}",
			Settings.Symbol,
			DescribePopulations(),
			Settings
		);
		Changed?.Invoke();
	}

	public void Dispose() => TryCancelLoop();

	private string DescribePopulations() =>
		string.Join(", ", Settings.Populations.Select(p => $"{p.Name}: {p.BotCount} bots × {p.StartingPoints:N0} points"));

	private async Task RunLoopAsync(CancellationToken token)
	{
		try
		{
			using var timer = new PeriodicTimer(Settings.StepInterval, _timeProvider);
			while (await timer.WaitForNextTickAsync(token))
			{
				Step();
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
		AppendHistory(new EmulatorPricePoint(now - Settings.StepInterval * Math.Max(0, Settings.WarmupSteps), broker.LastPrice));
		for (var step = Settings.WarmupSteps - 1; step >= 0; step--)
		{
			AdvanceMarket(broker, now - Settings.StepInterval * step);
		}

		return (broker, BuildSnapshot(broker, 0));
	}

	private void AdvanceMarket(EmulatorBroker broker, DateTime timestamp)
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
		AppendHistory(new EmulatorPricePoint(timestamp, broker.LastPrice));
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
