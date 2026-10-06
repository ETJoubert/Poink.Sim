using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Poink.Sim.Emulator;

/// <summary>
/// Process-wide emulated market for one synthetic asset. Each step moves the price, rebuilds the book and its bot population
/// around it and appends the price to a bounded history; the snapshot is replaced whole so readers never see a half-step.
/// </summary>
public sealed class EmulatorService : IEmulatorService, IDisposable
{
	private readonly ISyntheticMarketGenerator _generator;
	private readonly ILogger<EmulatorService> _logger;
	private readonly TimeProvider _timeProvider;
	private readonly object _lock = new();
	private readonly Queue<EmulatorPricePoint> _history = new();

	private EmulatorSnapshot _snapshot;
	private CancellationTokenSource? _loopCancellation;

	public EmulatorService(ISyntheticMarketGenerator generator, ILogger<EmulatorService> logger)
		: this(generator, logger, new EmulatorSettings(), TimeProvider.System) { }

	internal EmulatorService(
		ISyntheticMarketGenerator generator,
		ILogger<EmulatorService> logger,
		EmulatorSettings settings,
		TimeProvider timeProvider
	)
	{
		_generator = generator;
		_logger = logger;
		_timeProvider = timeProvider;
		Settings = settings;
		_snapshot = BuildWarmedUpSnapshot();
	}

	public EmulatorSettings Settings { get; }

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
			var price = _generator.NextPrice(_snapshot.Price, Settings);
			AppendHistory(new EmulatorPricePoint(_timeProvider.GetUtcNow().UtcDateTime, price));
			_snapshot = BuildSnapshot(price, _snapshot.Step + 1);
		}

		Changed?.Invoke();
	}

	public void Reset()
	{
		lock (_lock)
		{
			_snapshot = BuildWarmedUpSnapshot();
		}

		_logger.LogInformation("Emulator reset for {Symbol} with {Settings}", Settings.Symbol, Settings);
		Changed?.Invoke();
	}

	public void Dispose() => TryCancelLoop();

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

	private EmulatorSnapshot BuildWarmedUpSnapshot()
	{
		_history.Clear();
		var now = _timeProvider.GetUtcNow().UtcDateTime;
		var warmupSteps = Math.Max(0, Settings.WarmupSteps);
		var price = Settings.StartPrice;
		for (var step = warmupSteps; step > 0; step--)
		{
			AppendHistory(new EmulatorPricePoint(now - Settings.StepInterval * step, price));
			price = _generator.NextPrice(price, Settings);
		}
		AppendHistory(new EmulatorPricePoint(now, price));

		return BuildSnapshot(price, 0);
	}

	private EmulatorSnapshot BuildSnapshot(double price, long step)
	{
		var book = _generator.BuildBook(price, Settings);
		return new EmulatorSnapshot(
			Settings.Symbol,
			price,
			book,
			_generator.BuildPopulation(book, Settings),
			_history.ToArray(),
			step
		);
	}

	private void AppendHistory(EmulatorPricePoint point)
	{
		_history.Enqueue(point);
		while (_history.Count > Math.Max(1, Settings.HistoryCapacity))
		{
			_history.Dequeue();
		}
	}
}
