namespace Poink.Sim.Models;

/// <summary>How the bots' trading accounts are set up -- applied to every bot when the user sets it.</summary>
internal sealed record BotAccountOptions(
	double StartingCash,
	int BotCount,
	double Leverage,
	double CommissionRatePercent,
	double StopOutLevelPercent,
	double PipSize
);
