using System;
using System.ComponentModel.DataAnnotations;

namespace Poink.Sim.Data.Entities
{
	/// <summary>One trading interval from ProtoOASymbol.Schedule, in seconds from Sunday 00:00 in SymbolMetadata.ScheduleTimeZone.</summary>
	public class SymbolScheduleInterval
	{
		[Key]
		public Guid Id { get; set; }

		public Guid SymbolMetadataId { get; set; }

		public int StartSecond { get; set; }

		public int EndSecond { get; set; }

		public SymbolMetadata? SymbolMetadata { get; set; }
	}
}
