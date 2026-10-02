using System;
using System.Collections.Generic;
using Poink.Sim.Helpers;

namespace Poink.Sim.Models
{
	/// <summary>
	/// The fixed set of scalar settings a signal dataset is built with -- bar size, the rule engine's window and grid,
	/// the In band and every signal threshold. A dataset holds the signals for exactly one profile; another profile is
	/// another dataset.
	/// </summary>
	public sealed record SignalProfile
	{
		public int BarSizeSeconds { get; init; } = 60;

		/// <summary>Bars the volume profile, value area and regime signals are measured over.</summary>
		public int BarWindow { get; init; } = 50;

		/// <summary>Bars back from the live edge the window ends.</summary>
		public int BarOffset { get; init; }

		/// <summary>How close, in pips, price must be to a baseline level to count as In it.</summary>
		public double InTolerancePips { get; init; } = 1;

		/// <summary>Top-percent cutoff the XGrid rank and delta rows use.</summary>
		public double XGridPercent { get; init; } = 10;

		/// <summary>XGrid row height, in pips.</summary>
		public int XGridPips { get; init; } = 20;

		/// <summary>XGrid columns.</summary>
		public int XGridCols { get; init; } = 20;

		public RuleSignalSettings Signals { get; init; } = new();
	}

	/// <summary>One signal row of a dataset and the bit it occupies in each stored point's Above/In/Below masks.</summary>
	public sealed record SignalCatalogEntry(
		int Index,
		string Label,
		RuleSignalGroup Group,
		string? AboveKey,
		string InKey,
		string? BelowKey,
		string? Description
	);

	/// <summary>Where a dataset build has got to; Building until it completes, fails or is cancelled.</summary>
	public enum SignalDatasetStatus
	{
		Building,
		Complete,
		Failed,
		Cancelled,
	}

	/// <summary>A stored signal dataset: what it was built from and with, and the signal rows its points hold.</summary>
	public sealed record SignalDatasetInfo(
		long Id,
		string Name,
		string Table,
		DateTime FromUtc,
		DateTime ToUtc,
		double PipSize,
		SignalProfile Profile,
		IReadOnlyList<SignalCatalogEntry> Catalog,
		long PointCount,
		SignalDatasetStatus Status,
		DateTime CreatedAtUtc,
		DateTime? CompletedAtUtc
	);

	/// <summary>The signals at one moment: the quote, the ATR, and one bit per catalog row in each of the Above/In/Below masks.</summary>
	internal readonly record struct SignalPoint(
		DateTime TimestampUtc,
		double Bid,
		double Ask,
		double? Atr,
		long AboveMask,
		long InMask,
		long BelowMask
	);

	/// <summary>What to build a signal dataset from.</summary>
	public sealed record SignalDatasetBuildRequest(string Name, string Table, DateTime FromUtc, DateTime ToUtc, double PipSize, SignalProfile Profile);

	/// <summary>How far a running dataset build has got.</summary>
	public sealed record SignalDatasetBuildProgress(
		string Name,
		DateTime FromUtc,
		DateTime ToUtc,
		DateTime? CurrentUtc,
		long PointCount,
		bool IsWarmingUp
	)
	{
		public double Fraction =>
			CurrentUtc is { } current && ToUtc > FromUtc
				? Math.Clamp((current - FromUtc).TotalSeconds / (ToUtc - FromUtc).TotalSeconds, 0, 1)
				: 0;
	}
}
