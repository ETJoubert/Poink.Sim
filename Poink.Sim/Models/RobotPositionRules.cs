namespace Poink.Sim.Models
{
	/// <summary>Where the robot places its stop loss.</summary>
	public enum RobotStopMode
	{
		/// <summary>The Positioning console's own stop-loss setting.</summary>
		Console,

		/// <summary>A multiple of the average true range away from entry.</summary>
		Atr,

		/// <summary>Just beyond the nearest value-area level (VAL, POC or VAH) on the losing side.</summary>
		Structure,
	}

	/// <summary>Where the robot places its take profit.</summary>
	public enum RobotTargetMode
	{
		/// <summary>The Positioning console's own take-profit setting.</summary>
		Console,

		/// <summary>A multiple of the stop distance (R) beyond entry.</summary>
		RMultiple,

		/// <summary>The nearest value-area level (VAL, POC or VAH) on the winning side.</summary>
		Structure,
	}

	/// <summary>
	/// Rule-based placement and management of the robot's positions, layered over the Positioning console: stops and
	/// targets from volatility or market structure, breakeven and trailing stops, and smaller size in a drawdown.
	/// With the modes on Console and every switch off, the robot positions exactly as the console says.
	/// </summary>
	public sealed record RobotPositionRules
	{
		public RobotStopMode StopMode { get; init; } = RobotStopMode.Console;

		/// <summary>Stop distance in ATRs when the stop mode is ATR, and the fallback when no structure level qualifies.</summary>
		public double StopAtrMultiple { get; init; } = 1.5;

		/// <summary>Pips beyond the structure level the stop sits, so a touch of the level itself does not stop the trade out.</summary>
		public double StructureBufferPips { get; init; } = 2;

		/// <summary>Nearest a structure level may be to entry, in ATRs, to be used for a stop or target; closer ones are skipped.</summary>
		public double StructureMinAtr { get; init; } = 0.5;

		public RobotTargetMode TargetMode { get; init; } = RobotTargetMode.Console;

		/// <summary>Target distance in R when the target mode is R multiple, and the fallback when no structure level qualifies.</summary>
		public double TargetR { get; init; } = 1.5;

		public bool BreakevenEnabled { get; init; }

		/// <summary>Open profit, in R, at which the stop moves to the entry price.</summary>
		public double BreakevenAtR { get; init; } = 1;

		public bool TrailEnabled { get; init; }

		/// <summary>How far behind the best price since entry the trailing stop follows, in ATRs.</summary>
		public double TrailAtrMultiple { get; init; } = 2;

		/// <summary>Open profit, in R, before the stop starts trailing.</summary>
		public double TrailStartR { get; init; } = 1;

		public bool DrawdownThrottleEnabled { get; init; }

		/// <summary>Drop below the account's peak equity, in percent, at which new positions are sized down.</summary>
		public double DrawdownPercent { get; init; } = 10;

		/// <summary>What new positions' size is multiplied by while the account is in that drawdown.</summary>
		public double DrawdownSizeMultiplier { get; init; } = 0.5;
	}
}
