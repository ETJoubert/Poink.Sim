using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Poink.Sim.Helpers
{
	internal sealed class LocalStorageHelper : BrowserStorageHelper, ILocalStorageHelper
	{
		public LocalStorageHelper(IJSRuntime jsRuntime, ILogger<LocalStorageHelper> logger)
			: base(jsRuntime, logger, "localStorage") { }
	}
}
