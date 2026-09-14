using System;

namespace Poink.Sim.Components.Pages.Trade
{
	internal sealed class TradeDraftService : ITradeDraftService
	{
		public event Action? Changed;

		public TradeDraft? Draft { get; private set; }

		public void Update(TradeDraft draft)
		{
			if (Draft == draft)
				return;

			Draft = draft;
			Changed?.Invoke();
		}

		public void Clear()
		{
			if (Draft is null)
				return;

			Draft = null;
			Changed?.Invoke();
		}
	}
}
