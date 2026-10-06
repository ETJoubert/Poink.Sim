using Poink.Sim.Emulator;
using Poink.Sim.Emulator.Broker;
using Xunit;

namespace Poink.Sim.Tests.Services.Emulator.Broker;

public class EmulatorBotAccountTests
{
	private static EmulatorBotAccount CreateAccount() =>
		new()
		{
			Id = 1,
			Population = "Test",
			StartingPoints = 1_000,
			Cash = 1_000,
		};

	[Fact]
	public void ApplyFill_AddingToAPositionAveragesTheEntry()
	{
		var account = CreateAccount();

		account.ApplyFill(OrderSide.Buy, 2, 10);
		account.ApplyFill(OrderSide.Buy, 2, 12);

		Assert.Equal((4L, 11.0), (account.Position, account.AverageEntryPrice));
		Assert.Equal(1_000, account.Balance);
		Assert.Equal(1_004, account.Equity(12));
	}

	[Fact]
	public void ApplyFill_ReducingRealisesIntoBalanceAndKeepsTheEntry()
	{
		var account = CreateAccount();
		account.ApplyFill(OrderSide.Buy, 4, 10);

		account.ApplyFill(OrderSide.Sell, 1, 13);

		Assert.Equal((3L, 10.0), (account.Position, account.AverageEntryPrice));
		Assert.Equal(1_003, account.Balance);
	}

	[Fact]
	public void ApplyFill_ClosingLeavesOnlyRealisedBalance()
	{
		var account = CreateAccount();
		account.ApplyFill(OrderSide.Sell, 5, 20);

		account.ApplyFill(OrderSide.Buy, 5, 18);

		Assert.Equal((0L, 0.0), (account.Position, account.AverageEntryPrice));
		Assert.Equal(1_010, account.Balance);
		Assert.Equal(account.Balance, account.Equity(99));
	}

	[Fact]
	public void ApplyFill_FlippingStartsANewEntryAtTheFillPrice()
	{
		var account = CreateAccount();
		account.ApplyFill(OrderSide.Buy, 2, 10);

		account.ApplyFill(OrderSide.Sell, 5, 11);

		Assert.Equal((-3L, 11.0), (account.Position, account.AverageEntryPrice));
		Assert.Equal(1_002, account.Balance);
	}

	[Fact]
	public void GoBust_ZeroesTheAccountAndStopsIt()
	{
		var account = CreateAccount();

		account.GoBust();

		Assert.True(account.IsBust);
		Assert.Equal(0, account.Balance);
	}
}
