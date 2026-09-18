namespace Poink.Sim.Services.DataSeriesSync;

internal interface IDataSeriesSyncService
{
	DataSeriesSyncRange GetSyncRange(string symbolName);
}
