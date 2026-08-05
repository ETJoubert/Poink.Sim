using Poink.Sim.Components.Pages.Code.Models;

namespace Poink.Sim.Components.Pages.Code.Services;

internal interface ICodeGraphProvider
{
    CodeGraph Build();
}
