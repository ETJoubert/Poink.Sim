
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Training;

internal sealed class RewardPolicyService : IRewardPolicyService
{
    private readonly object _lock = new();
    private readonly List<IRewardPolicy> _policies;
    private IRewardPolicy _selected;

    public RewardPolicyService()
    {
        _policies = [new EquityDriftRewardPolicy()];
        _selected = _policies.First();
    }

    public IReadOnlyList<string> PolicyNames => _policies.Select(p => p.Name).ToList();

    public string SelectedPolicyName { get { lock (_lock) { return _selected.Name; } } }

    public IRewardPolicy SelectedPolicy { get { lock (_lock) { return _selected; } } }

    public void SelectPolicy(string name)
    {
        var match = _policies.FirstOrDefault(p => p.Name == name);
        if (match is null)
            return;
        lock (_lock)
        { _selected = match; }
    }
}
