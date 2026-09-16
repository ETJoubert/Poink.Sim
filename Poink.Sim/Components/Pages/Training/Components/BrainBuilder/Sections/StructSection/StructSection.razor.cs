using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
	public partial class StructSection : ComponentBase
	{
		[Inject]
		private ITrainingService Service { get; set; } = default!;

		[Parameter]
		public BrainBuilderState State { get; set; } = default!;

		[Parameter]
		public EventCallback OnChanged { get; set; }

		[Parameter]
		public int TotalInputCount { get; set; }

		private Task NotifyChanged() => OnChanged.InvokeAsync();

		private int? FirstHiddenLayerSize =>
			State.ArchitectureKind switch
			{
				ArchitectureKind.Mlp => State.MlpHiddenSizes.Where(n => n > 0).ToList() is { Count: > 0 } sizes ? sizes[0] : null,
				ArchitectureKind.Cnn => State.CnnChannels.Where(n => n > 0).ToList() is { Count: > 0 } channels ? channels[0] : null,
				_ => State.LstmHiddenSize,
			};

		private bool FirstLayerTooSmall => FirstHiddenLayerSize is int size && size < TotalInputCount;

		private Task AddHiddenLayer()
		{
			State.MlpHiddenSizes.Add(Math.Max(1, State.MlpHiddenSizes[^1] / 2));
			return NotifyChanged();
		}

		private Task AddHiddenLayerOnTop()
		{
			State.MlpHiddenSizes.Insert(0, State.MlpHiddenSizes[0] * 2);
			return NotifyChanged();
		}

		private Task RemoveHiddenLayer(int index)
		{
			if (State.MlpHiddenSizes.Count <= 1)
				return Task.CompletedTask;
			State.MlpHiddenSizes.RemoveAt(index);
			return NotifyChanged();
		}

		private bool CanRemoveHiddenLayer => State.MlpHiddenSizes.Count > 1;

		private Task OnHiddenLayerSizeChanged(int index, ChangeEventArgs e)
		{
			if (int.TryParse(e.Value?.ToString(), out int size))
				State.MlpHiddenSizes[index] = size;
			return NotifyChanged();
		}

		private Task AddCnnChannelLayer()
		{
			State.CnnChannels.Add(State.CnnChannels[^1] * 2);
			return NotifyChanged();
		}

		private Task RemoveCnnChannelLayer(int index)
		{
			if (State.CnnChannels.Count <= 1)
				return Task.CompletedTask;
			State.CnnChannels.RemoveAt(index);
			return NotifyChanged();
		}

		private bool CanRemoveCnnChannelLayer => State.CnnChannels.Count > 1;

		private Task OnCnnChannelSizeChanged(int index, ChangeEventArgs e)
		{
			if (int.TryParse(e.Value?.ToString(), out int size))
				State.CnnChannels[index] = size;
			return NotifyChanged();
		}

		private bool CnnSweepSourceIncluded =>
			State.CnnKernelSweepSource == CnnKernelSweepSource.VolumeProfile ? State.IncludeVolumeProfile : State.IncludeBarWindow;
	}
}
