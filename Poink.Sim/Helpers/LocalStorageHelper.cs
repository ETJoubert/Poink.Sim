using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Poink.Sim.Helpers
{
	internal sealed class LocalStorageHelper : ILocalStorageHelper
	{
		private readonly IJSRuntime _jsRuntime;
		private readonly ILogger<LocalStorageHelper> _logger;

		public LocalStorageHelper(IJSRuntime jsRuntime, ILogger<LocalStorageHelper> logger)
		{
			_jsRuntime = jsRuntime;
			_logger = logger;
		}

		public async Task SetItemAsync<T>(string key, T value) =>
			await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value));

		public async Task<T?> GetItemAsync<T>(string key)
		{
			var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
			if (string.IsNullOrEmpty(json))
				return default;

			try
			{
				return JsonSerializer.Deserialize<T>(json);
			}
			catch (JsonException ex)
			{
				_logger.LogWarning(ex, "Failed to deserialize localStorage entry '{Key}'; ignoring stored value", key);
				return default;
			}
		}
	}
}
