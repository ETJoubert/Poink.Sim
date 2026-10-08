using System.Threading.Tasks;

namespace Poink.Sim.Helpers
{
	/// <summary>
	/// Browser sessionStorage access, JSON-serialized per value: kept across reloads of the same tab, dropped when the tab
	/// closes. Backed by JS interop, so a call only succeeds once the component has rendered at least once (not from
	/// OnInitializedAsync).
	/// </summary>
	internal interface ISessionStorageHelper
	{
		Task SetItemAsync<T>(string key, T value);

		Task<T?> GetItemAsync<T>(string key);
	}
}
