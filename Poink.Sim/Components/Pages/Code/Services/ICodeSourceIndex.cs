using System;

namespace Poink.Sim.Components.Pages.Code.Services;

internal interface ICodeSourceIndex
{
    string? PathFor(Type type);
}
