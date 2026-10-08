using System;
using System.Threading;

namespace Poink.Sim.Tests.TestSupport;

/// <summary>A clock the test moves by hand; its timers never fire on their own, so time-driven work runs only when the test calls it.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
	public DateTimeOffset Now { get; private set; } = start;

	public override DateTimeOffset GetUtcNow() => Now;

	public void Advance(TimeSpan by) => Now += by;

	public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new IdleTimer();

	private sealed class IdleTimer : ITimer
	{
		public bool Change(TimeSpan dueTime, TimeSpan period) => true;

		public void Dispose() { }

		public System.Threading.Tasks.ValueTask DisposeAsync() => default;
	}
}
