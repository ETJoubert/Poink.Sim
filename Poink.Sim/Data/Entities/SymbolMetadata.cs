using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Poink.Sim.Data.Entities
{
	/// <summary>
	/// Full per-symbol trading detail from cTrader's ProtoOASymbol (digits, volume/commission/swap
	/// rules), correlated to <see cref="Symbol"/> by <see cref="SymbolId"/> rather than a DB-enforced
	/// foreign key, matching how the rest of the app correlates cTrader data by symbol id.
	/// </summary>
	[Index(nameof(SymbolId), IsUnique = true)]
	public class SymbolMetadata
	{
		[Key]
		public Guid Id { get; set; }

		public long SymbolId { get; set; }

		public int Digits { get; set; }

		public int PipPosition { get; set; }

		public bool EnableShortSelling { get; set; }

		public bool GuaranteedStopLoss { get; set; }

		public ProtoOADayOfWeek SwapRollover3Days { get; set; }

		public double SwapLong { get; set; }

		public double SwapShort { get; set; }

		public long MaxVolume { get; set; }

		public long MinVolume { get; set; }

		public long StepVolume { get; set; }

		public long MaxExposure { get; set; }

		public ProtoOACommissionType CommissionType { get; set; }

		public int SlDistance { get; set; }

		public int TpDistance { get; set; }

		public int GslDistance { get; set; }

		public long GslCharge { get; set; }

		public ProtoOASymbolDistanceType DistanceSetIn { get; set; }

		public ProtoOAMinCommissionType MinCommissionType { get; set; }

		public string MinCommissionAsset { get; set; } = string.Empty;

		public long RolloverCommission { get; set; }

		public int SkipRolloverDays { get; set; }

		public string ScheduleTimeZone { get; set; } = string.Empty;

		public ProtoOATradingMode TradingMode { get; set; }

		public ProtoOADayOfWeek RolloverCommission3Days { get; set; }

		public ProtoOASwapCalculationType SwapCalculationType { get; set; }

		public long LotSize { get; set; }

		public long PreciseTradingCommissionRate { get; set; }

		public long PreciseMinCommission { get; set; }

		public int PnlConversionFeeRate { get; set; }

		public long LeverageId { get; set; }

		public int SwapPeriod { get; set; }

		public int SwapTime { get; set; }

		public int SkipSWAPPeriods { get; set; }

		public bool ChargeSwapAtWeekends { get; set; }

		public string MeasurementUnits { get; set; } = string.Empty;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public DateTime? ChangedAt { get; set; }

		public List<SymbolScheduleInterval> ScheduleIntervals { get; set; } = new();

		public List<SymbolHoliday> Holidays { get; set; } = new();
	}
}
