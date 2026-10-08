using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Poink.Sim.Helpers
{
	/// <summary>
	/// JSON-serialized access to one of the browser's Web Storage areas (localStorage or sessionStorage). Backed by JS
	/// interop, so a call only succeeds once the component has rendered at least once.
	/// </summary>
	internal abstract class BrowserStorageHelper
	{
		private readonly IJSRuntime _jsRuntime;
		private readonly ILogger _logger;
		private readonly string _storageName;

		protected BrowserStorageHelper(IJSRuntime jsRuntime, ILogger logger, string storageName)
		{
			_jsRuntime = jsRuntime;
			_logger = logger;
			_storageName = storageName;
		}

		public async Task SetItemAsync<T>(string key, T value) =>
			await _jsRuntime.InvokeVoidAsync($"{_storageName}.setItem", key, JsonSerializer.Serialize(value));

		public async Task<T?> GetItemAsync<T>(string key)
		{
			var json = await _jsRuntime.InvokeAsync<string?>($"{_storageName}.getItem", key);
			if (string.IsNullOrEmpty(json))
				return default;

			try
			{
				return JsonSerializer.Deserialize<T>(json);
			}
			catch (JsonException ex)
			{
				_logger.LogWarning(ex, "Failed to deserialize {Storage} entry '{Key}'; ignoring stored value", _storageName, key);
				return default;
			}
		}
	}
}
