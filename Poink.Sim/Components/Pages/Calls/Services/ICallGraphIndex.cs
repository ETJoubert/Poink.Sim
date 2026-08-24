using System.Threading.Tasks;
using Poink.Sim.Components.Pages.Calls.Models;

namespace Poink.Sim.Components.Pages.Calls.Services;

internal interface ICallGraphIndex
{
    Task<CallGraphIndexResult> GetAsync();
}
