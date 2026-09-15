using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Poink.ZTicks;

/// <summary>Decodes cTrader's .zticks cache: gzip wrapping 24-byte records (unix-ms, bid*1e5, ask*1e5), reverse-engineered.</summary>
public static class ZTicksFileReader
{
	private const int RecordSize = 24;
	private const double PriceScale = 100000.0;

	public static IReadOnlyList<TickRecord> Read(string path)
	{
		using var fileStream = File.OpenRead(path);
		using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
		using var buffer = new MemoryStream();
		gzipStream.CopyTo(buffer);
		var data = buffer.ToArray();

		if (data.Length % RecordSize != 0)
		{
			throw new InvalidDataException(
				$"'{path}' decompressed to {data.Length} bytes, not a multiple of the {RecordSize}-byte record size."
			);
		}

		var count = data.Length / RecordSize;
		var ticks = new List<TickRecord>(count);

		// Each record updates only one side; the other arrives as a raw 0 -- carry the last value forward.
		double? lastBid = null;
		double? lastAsk = null;

		for (var i = 0; i < count; i++)
		{
			var offset = i * RecordSize;
			var timestampMs = BitConverter.ToInt64(data, offset);
			var bidRaw = BitConverter.ToInt64(data, offset + 8);
			var askRaw = BitConverter.ToInt64(data, offset + 16);

			if (bidRaw != 0)
				lastBid = bidRaw / PriceScale;
			if (askRaw != 0)
				lastAsk = askRaw / PriceScale;

			if (lastBid is null || lastAsk is null)
				continue;

			ticks.Add(new TickRecord(DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime, lastBid.Value, lastAsk.Value));
		}

		return ticks;
	}
}
