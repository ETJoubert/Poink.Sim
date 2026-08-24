using System.Collections.Generic;

namespace Poink.Sim.Training;

internal interface IRewardPolicyService
{
	IReadOnlyList<string> PolicyNames { get; }
	string SelectedPolicyName { get; }
	IRewardPolicy SelectedPolicy { get; }

	void SelectPolicy(string name);
}
