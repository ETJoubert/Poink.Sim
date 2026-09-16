using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
	public partial class LearnSection : ComponentBase
	{
		[Inject]
		private ITrainingService Service { get; set; } = default!;

		[Inject]
		private IRewardPolicyService RewardPolicyService { get; set; } = default!;

		[Parameter]
		public string StrategyName { get; set; } = "";

		[Parameter]
		public EventCallback<string> StrategyNameChanged { get; set; }

		[Parameter]
		public double LearningRate { get; set; }

		[Parameter]
		public EventCallback<double> LearningRateChanged { get; set; }

		private Task OnStrategyChanged(ChangeEventArgs e) => StrategyNameChanged.InvokeAsync(e.Value?.ToString() ?? "");

		private void OnRewardPolicyChanged(ChangeEventArgs e) =>
			RewardPolicyService.SelectPolicy(e.Value?.ToString() ?? "");

		private Task OnLearningRateChanged(ChangeEventArgs e) =>
			double.TryParse(e.Value?.ToString(), out var rate) ? LearningRateChanged.InvokeAsync(rate) : Task.CompletedTask;
	}
}
