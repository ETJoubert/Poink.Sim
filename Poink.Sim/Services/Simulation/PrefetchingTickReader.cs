using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Poink.Sim.Repository;

namespace Poink.Sim.Simulation;

/// <summary>
/// Reads ticks from a source enumerator on a background task in fixed-size blocks, so fetching the next
/// ticks overlaps with replaying the current ones. Yields exactly the source's ticks, in order.
/// </summary>
internal sealed class PrefetchingTickReader : IEnumerator<Tick>
{
	private const int BlockSize = 4096;
	private const int BlocksAhead = 8;

	private readonly Channel<Tick[]> _blocks = Channel.CreateBounded<Tick[]>(
		new BoundedChannelOptions(BlocksAhead) { SingleReader = true, SingleWriter = true }
	);
	private readonly CancellationTokenSource _cancellation = new();

	private Tick[] _block = Array.Empty<Tick>();
	private int _index = -1;

	public PrefetchingTickReader(Func<IEnumerator<Tick>> openSource) =>
		_ = Task.Run(() => FillAsync(openSource, _cancellation.Token));

	public Tick Current => _block[_index];

	object IEnumerator.Current => Current;

	public bool MoveNext()
	{
		if (++_index < _block.Length)
			return true;

		while (!_blocks.Reader.TryRead(out _block!))
		{
			if (!_blocks.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
			{
				_block = Array.Empty<Tick>();
				_index = -1;
				return false;
			}
		}
		_index = 0;
		return true;
	}

	public void Reset() => throw new NotSupportedException();

	public void Dispose() => _cancellation.Cancel();

	// The source is opened, read and disposed on this task alone, since a database reader is not thread-safe.
	private async Task FillAsync(Func<IEnumerator<Tick>> openSource, CancellationToken cancellation)
	{
		try
		{
			using var source = openSource();
			var block = new Tick[BlockSize];
			int count = 0;
			while (!cancellation.IsCancellationRequested && source.MoveNext())
			{
				block[count++] = source.Current;
				if (count < BlockSize)
					continue;
				await _blocks.Writer.WriteAsync(block, cancellation);
				block = new Tick[BlockSize];
				count = 0;
			}
			if (count > 0 && !cancellation.IsCancellationRequested)
				await _blocks.Writer.WriteAsync(block[..count], cancellation);
			_blocks.Writer.TryComplete();
		}
		catch (OperationCanceledException)
		{
			_blocks.Writer.TryComplete();
		}
		catch (Exception ex)
		{
			_blocks.Writer.TryComplete(ex);
		}
	}
}
