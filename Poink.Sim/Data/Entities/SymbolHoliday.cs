using System;
using System.ComponentModel.DataAnnotations;

namespace Poink.Sim.Data.Entities
{
	/// <summary>One broker-specified holiday from ProtoOASymbol.Holiday.</summary>
	public class SymbolHoliday
	{
		[Key]
		public Guid Id { get; set; }

		public Guid SymbolMetadataId { get; set; }

		public long HolidayId { get; set; }

		public string Name { get; set; } = string.Empty;

		public string Description { get; set; } = string.Empty;

		public string ScheduleTimeZone { get; set; } = string.Empty;

		public DateOnly HolidayDate { get; set; }

		public bool IsRecurring { get; set; }

		public int StartSecond { get; set; }

		public int EndSecond { get; set; }

		public SymbolMetadata? SymbolMetadata { get; set; }
	}
}
