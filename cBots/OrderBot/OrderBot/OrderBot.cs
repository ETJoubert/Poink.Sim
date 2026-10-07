namespace cAlgo.Robots;

/// <summary>
/// Executes trades for Poink.Sim on the account it is attached to, through the cTrader Automate channel (the broker
/// blocks Open API trading on live). With <c>AcceptExternalCommands</c> on it runs the localhost HTTP bridge;
/// otherwise it runs the one-shot channel test. A live account additionally requires <c>AllowLive</c>.
/// </summary>
public partial class OrderBot : Robot
{
	private long _accountNumber;
	private bool _accountIsLive;
	private string _brokerName;

	protected override void OnStart()
	{
		_accountNumber = Account.Number;
		_accountIsLive = Account.IsLive;
		_brokerName = Account.BrokerName;

		Print(
			"Account {0} ({1}) at {2}, balance {3} {4}.",
			_accountNumber,
			_accountIsLive ? "LIVE" : "DEMO",
			_brokerName,
			Account.Balance,
			Account.Asset.Name
		);

		if (_accountIsLive && !AllowLive)
		{
			Print(
				"Live account detected and AllowLive is false: nothing will trade. Set AllowLive to true to continue."
			);
			Stop();
			return;
		}

		if (AcceptExternalCommands)
			StartBridge();
		else
			RunChannelTest();
	}

	protected override void OnStop()
	{
		StopBridge();
		CloseTestPosition();
	}
}
