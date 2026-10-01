using System;

namespace Poink.Sim.Models.TrainingModels
{
	/// <summary>
	/// The Data tab's selected tick source -- cached table, date range, and symbol. Symbol is stored
	/// by id; its trading metadata is resolved separately via ISymbolMetadataCache.
	/// </summary>
	internal sealed record DataOptionsModel(string Table, DateTime From, DateTime To, long? SymbolId = null, double PipSize = 0.0001);
}
