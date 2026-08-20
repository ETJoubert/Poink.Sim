using System.Threading.Tasks;

namespace Poink.Sim.Helpers
{
    /// <summary>
    /// Browser localStorage access, JSON-serialized per value. Backed by JS interop, so a
    /// call only succeeds once the component has rendered at least once (not from
    /// OnInitializedAsync).
    /// </summary>
    internal interface ILocalStorageHelper
    {
        Task SetItemAsync<T>(string key, T value);

        Task<T?> GetItemAsync<T>(string key);
    }
}
