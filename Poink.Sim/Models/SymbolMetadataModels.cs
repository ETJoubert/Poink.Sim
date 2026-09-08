using System;
using System.Collections.Generic;

namespace Poink.Sim.Models
{
	public sealed record SymbolScheduleIntervalModel(int StartSecond, int EndSecond);

	public sealed record SymbolHolidayModel(
		long HolidayId,
		string Name,
		string Description,
		string ScheduleTimeZone,
		DateOnly HolidayDate,
		bool IsRecurring,
		int StartSecond,
		int EndSecond
	);

	public sealed record SymbolMetadataModel(
		Guid Id,
		long SymbolId,
		int Digits,
		int PipPosition,
		bool EnableShortSelling,
		bool GuaranteedStopLoss,
		ProtoOADayOfWeek SwapRollover3Days,
		double SwapLong,
		double SwapShort,
		long MaxVolume,
		long MinVolume,
		long StepVolume,
		long MaxExposure,
		ProtoOACommissionType CommissionType,
		int SlDistance,
		int TpDistance,
		int GslDistance,
		long GslCharge,
		ProtoOASymbolDistanceType DistanceSetIn,
		ProtoOAMinCommissionType MinCommissionType,
		string MinCommissionAsset,
		long RolloverCommission,
		int SkipRolloverDays,
		string ScheduleTimeZone,
		ProtoOATradingMode TradingMode,
		ProtoOADayOfWeek RolloverCommission3Days,
		ProtoOASwapCalculationType SwapCalculationType,
		long LotSize,
		long PreciseTradingCommissionRate,
		long PreciseMinCommission,
		int PnlConversionFeeRate,
		long LeverageId,
		int SwapPeriod,
		int SwapTime,
		int SkipSWAPPeriods,
		bool ChargeSwapAtWeekends,
		string MeasurementUnits,
		DateTime CreatedAt,
		DateTime? ChangedAt,
		IReadOnlyList<SymbolScheduleIntervalModel> ScheduleIntervals,
		IReadOnlyList<SymbolHolidayModel> Holidays
	);
}
