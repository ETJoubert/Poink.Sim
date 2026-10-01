using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>Turns OutputOptionsModel into an ActionSpace -- which directions are enabled, in what order, and how outputs decode.</summary>
internal sealed class ActionAssembler
{
	public ActionSpace Assemble(OutputOptionsModel options) =>
		new(
			EnabledDirections(options.Actions),
			options.ActionDecoding.DecisionDecoding,
			options.ActionDecoding.DecisionTemperature
		);

	private static IReadOnlyList<SimulationAction> EnabledDirections(ActionsOptionsModel actions)
	{
		var directions = new List<SimulationAction>();
		if (actions.IncludeHold)
			directions.Add(SimulationAction.Hold);
		if (actions.IncludeEnterLong)
			directions.Add(SimulationAction.EnterLong);
		if (actions.IncludeEnterShort)
			directions.Add(SimulationAction.EnterShort);
		if (actions.IncludeClose)
			directions.Add(SimulationAction.Close);
		return directions;
	}
}
