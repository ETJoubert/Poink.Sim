namespace Poink.Sim.Trader.Commission;

internal interface ICommissionModel
{
	double CommissionFor(double size, double fillPrice);
}
