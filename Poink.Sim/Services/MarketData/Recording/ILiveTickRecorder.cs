using System;

namespace Poink.Sim.Services.MarketData.Recording
{
	/// <summary>
	/// Commits the session's streamed ticks to the tick tables while at least one lease is held (the Trade page holds
	/// one while open), so today's database coverage grows without repeat API calls.
	/// </summary>
	internal interface ILiveTickRecorder
	{
		bool IsRecording { get; }

		/// <summary>Starts recording on the first lease; disposing the last lease flushes and stops.</summary>
		IAsyncDisposable Acquire();

		/// <summary>Drops buffered, unwritten ticks for <paramref name="table"/> older than <paramref name="beforeUtc"/>, after that range was replaced in the table.</summary>
		int DiscardBuffered(string table, DateTime beforeUtc);
	}
}
