using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Poink.Sim.Helpers
{
	internal sealed class SessionStorageHelper : BrowserStorageHelper, ISessionStorageHelper
	{
		public SessionStorageHelper(IJSRuntime jsRuntime, ILogger<SessionStorageHelper> logger)
			: base(jsRuntime, logger, "sessionStorage") { }
	}
}
