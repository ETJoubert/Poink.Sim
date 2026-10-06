namespace Poink.Sim.Emulator.Bots;

/// <summary>Decides one bot's action from what that bot alone can see.</summary>
public interface IEmulatorBotStrategy
{
	BotDecision Decide(BotMarketView view, EmulatorSettings settings);
}
