using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Services.Activity;

namespace Poink.Sim.Components.Shared.Toast
{
	/// <summary>
	/// Pops a toast for every background run that completes or fails, linking back to the tab it ran on.
	/// </summary>
	public partial class ActivityToastHost : ComponentBase, IDisposable
	{
		private const int MaxVisibleToasts = 4;
		private static readonly TimeSpan ToastLifetime = TimeSpan.FromSeconds(8);

		[Inject]
		private IActivityTracker ActivityTracker { get; set; } = default!;

		[Inject]
		private NavigationManager Navigation { get; set; } = default!;

		private readonly List<ActivityToast> _toasts = new();
		private readonly CancellationTokenSource _disposal = new();
		private int _nextId;

		protected override void OnInitialized() => ActivityTracker.Completed += HandleCompleted;

		private void HandleCompleted(ActivityCompletion completion) => _ = InvokeAsync(() => Show(completion));

		private async Task Show(ActivityCompletion completion)
		{
			var toast = new ActivityToast(++_nextId, completion);
			_toasts.Add(toast);
			if (_toasts.Count > MaxVisibleToasts)
				_toasts.RemoveAt(0);
			StateHasChanged();

			try
			{
				await Task.Delay(ToastLifetime, _disposal.Token);
			}
			catch (TaskCanceledException)
			{
				return;
			}
			Dismiss(toast);
		}

		private void Open(ActivityToast toast)
		{
			Dismiss(toast);
			Navigation.NavigateTo(toast.Completion.Route);
		}

		private void Dismiss(ActivityToast toast)
		{
			if (_toasts.Remove(toast))
				StateHasChanged();
		}

		public void Dispose()
		{
			ActivityTracker.Completed -= HandleCompleted;
			_disposal.Cancel();
		}

		private sealed record ActivityToast(int Id, ActivityCompletion Completion);
	}
}
