using System;

namespace Poink.Sim.Components.Pages.Arch.Services;

public interface IXmlDocumentationProvider
{
	string? SummaryFor(Type type);
}
