using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Poink.Sim.Data;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Services.Symbols
{
	public sealed class SymbolService : ISymbolService
	{
		private readonly IDbContextFactory<PoinkSimContext> _contextFactory;
		private readonly ICTraderSessionService _session;
		private readonly ILogger<SymbolService> _logger;

		public SymbolService(
			IDbContextFactory<PoinkSimContext> contextFactory,
			ICTraderSessionService session,
			ILogger<SymbolService> logger
		)
		{
			_contextFactory = contextFactory;
			_session = session;
			_logger = logger;
		}

		public async Task<IReadOnlyList<SymbolModel>> ListAsync(CancellationToken cancellationToken = default)
		{
			await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
			var entities = await context.Symbols.OrderBy(s => s.SymbolName).ToListAsync(cancellationToken);

			return entities.Select(ToModel).ToList();
		}

		public async Task<IReadOnlyList<SymbolModel>> RefreshFromCTraderAsync(
			CancellationToken cancellationToken = default
		)
		{
			_logger.LogInformation("Refreshing symbols from cTrader...");

			var fetched = await _session.RefreshSymbolsAsync(cancellationToken);

			await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
			var existing = await context.Symbols.ToDictionaryAsync(s => s.SymbolId, cancellationToken);
			var now = DateTime.UtcNow;

			foreach (var symbol in fetched)
			{
				if (existing.TryGetValue(symbol.SymbolId, out var entity))
				{
					entity.SymbolName = symbol.SymbolName;
					entity.Enabled = symbol.Enabled;
					entity.BaseAssetId = symbol.BaseAssetId;
					entity.QuoteAssetId = symbol.QuoteAssetId;
					entity.SymbolCategoryId = symbol.SymbolCategoryId;
					entity.Description = symbol.Description;
					entity.SortingNumber = symbol.SortingNumber;
					entity.ChangedAt = now;
				}
				else
				{
					context.Symbols.Add(
						new Data.Entities.Symbol
						{
							Id = Guid.NewGuid(),
							SymbolId = symbol.SymbolId,
							SymbolName = symbol.SymbolName,
							Enabled = symbol.Enabled,
							BaseAssetId = symbol.BaseAssetId,
							QuoteAssetId = symbol.QuoteAssetId,
							SymbolCategoryId = symbol.SymbolCategoryId,
							Description = symbol.Description,
							SortingNumber = symbol.SortingNumber,
							CreatedAt = now,
						}
					);
				}
			}

			await context.SaveChangesAsync(cancellationToken);

			_logger.LogInformation("Refreshed {SymbolCount} symbol(s) from cTrader.", fetched.Count);

			return await ListAsync(cancellationToken);
		}

		private static SymbolModel ToModel(Data.Entities.Symbol entity) =>
			new(
				entity.Id,
				entity.SymbolId,
				entity.SymbolName,
				entity.Enabled,
				entity.BaseAssetId,
				entity.QuoteAssetId,
				entity.SymbolCategoryId,
				entity.Description,
				entity.SortingNumber,
				entity.CreatedAt,
				entity.ChangedAt
			);

		public async Task<SymbolMetadataModel?> GetMetadataAsync(
			long symbolId,
			CancellationToken cancellationToken = default
		)
		{
			await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
			var entity = await context
				.SymbolMetadata.Include(m => m.ScheduleIntervals)
				.Include(m => m.Holidays)
				.FirstOrDefaultAsync(m => m.SymbolId == symbolId, cancellationToken);

			return entity is null ? null : ToMetadataModel(entity);
		}

		public async Task<SymbolMetadataModel?> RefreshMetadataFromCTraderAsync(
			long symbolId,
			CancellationToken cancellationToken = default
		)
		{
			_logger.LogInformation("Refreshing metadata for symbol {SymbolId} from cTrader...", symbolId);

			var fetched = await _session.GetSymbolMetadataAsync(symbolId, cancellationToken);
			if (fetched is null)
			{
				_logger.LogWarning("cTrader returned no metadata for symbol {SymbolId}.", symbolId);
				return null;
			}

			await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
			var entity = await context
				.SymbolMetadata.Include(m => m.ScheduleIntervals)
				.Include(m => m.Holidays)
				.FirstOrDefaultAsync(m => m.SymbolId == symbolId, cancellationToken);

			var now = DateTime.UtcNow;
			if (entity is null)
			{
				entity = new Data.Entities.SymbolMetadata { Id = Guid.NewGuid(), SymbolId = symbolId, CreatedAt = now };
				context.SymbolMetadata.Add(entity);
			}
			else
			{
				entity.ScheduleIntervals.Clear();
				entity.Holidays.Clear();
				entity.ChangedAt = now;
			}

			entity.Digits = fetched.Digits;
			entity.PipPosition = fetched.PipPosition;
			entity.EnableShortSelling = fetched.EnableShortSelling;
			entity.GuaranteedStopLoss = fetched.GuaranteedStopLoss;
			entity.SwapRollover3Days = fetched.SwapRollover3Days;
			entity.SwapLong = fetched.SwapLong;
			entity.SwapShort = fetched.SwapShort;
			entity.MaxVolume = fetched.MaxVolume;
			entity.MinVolume = fetched.MinVolume;
			entity.StepVolume = fetched.StepVolume;
			entity.MaxExposure = (long)fetched.MaxExposure;
			entity.CommissionType = fetched.CommissionType;
			entity.SlDistance = (int)fetched.SlDistance;
			entity.TpDistance = (int)fetched.TpDistance;
			entity.GslDistance = (int)fetched.GslDistance;
			entity.GslCharge = fetched.GslCharge;
			entity.DistanceSetIn = fetched.DistanceSetIn;
			entity.MinCommissionType = fetched.MinCommissionType;
			entity.MinCommissionAsset = fetched.MinCommissionAsset;
			entity.RolloverCommission = fetched.RolloverCommission;
			entity.SkipRolloverDays = fetched.SkipRolloverDays;
			entity.ScheduleTimeZone = fetched.ScheduleTimeZone;
			entity.TradingMode = fetched.TradingMode;
			entity.RolloverCommission3Days = fetched.RolloverCommission3Days;
			entity.SwapCalculationType = fetched.SwapCalculationType;
			entity.LotSize = fetched.LotSize;
			entity.PreciseTradingCommissionRate = fetched.PreciseTradingCommissionRate;
			entity.PreciseMinCommission = fetched.PreciseMinCommission;
			entity.PnlConversionFeeRate = fetched.PnlConversionFeeRate;
			entity.LeverageId = fetched.LeverageId;
			entity.SwapPeriod = fetched.SwapPeriod;
			entity.SwapTime = fetched.SwapTime;
			entity.SkipSWAPPeriods = fetched.SkipSWAPPeriods;
			entity.ChargeSwapAtWeekends = fetched.ChargeSwapAtWeekends;
			entity.MeasurementUnits = fetched.MeasurementUnits;

			foreach (var interval in fetched.Schedule)
			{
				entity.ScheduleIntervals.Add(
					new Data.Entities.SymbolScheduleInterval
					{
						Id = Guid.NewGuid(),
						SymbolMetadataId = entity.Id,
						StartSecond = (int)interval.StartSecond,
						EndSecond = (int)interval.EndSecond,
					}
				);
			}

			foreach (var holiday in fetched.Holiday)
			{
				entity.Holidays.Add(
					new Data.Entities.SymbolHoliday
					{
						Id = Guid.NewGuid(),
						SymbolMetadataId = entity.Id,
						HolidayId = holiday.HolidayId,
						Name = holiday.Name,
						Description = holiday.Description,
						ScheduleTimeZone = holiday.ScheduleTimeZone,
						HolidayDate = DateOnly.FromDateTime(DateTime.UnixEpoch.AddDays(holiday.HolidayDate)),
						IsRecurring = holiday.IsRecurring,
						StartSecond = holiday.StartSecond,
						EndSecond = holiday.EndSecond,
					}
				);
			}

			await context.SaveChangesAsync(cancellationToken);

			_logger.LogInformation("Refreshed metadata for symbol {SymbolId}.", symbolId);

			return ToMetadataModel(entity);
		}

		public async Task<IReadOnlyList<SymbolCategoryModel>> ListCategoriesAsync(CancellationToken cancellationToken = default)
		{
			await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
			var entities = await context.SymbolCategories.OrderBy(c => c.Name).ToListAsync(cancellationToken);

			return entities.Select(ToCategoryModel).ToList();
		}

		public async Task<IReadOnlyList<SymbolCategoryModel>> RefreshCategoriesFromCTraderAsync(
			CancellationToken cancellationToken = default
		)
		{
			_logger.LogInformation("Refreshing symbol categories from cTrader...");

			var (categories, assetClasses) = await _session.GetSymbolCategoriesAsync(cancellationToken);
			var assetClassNames = assetClasses.ToDictionary(a => a.Id, a => a.Name);

			await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
			var existing = await context.SymbolCategories.ToDictionaryAsync(c => c.SymbolCategoryId, cancellationToken);
			var now = DateTime.UtcNow;

			foreach (var category in categories)
			{
				var assetClassName = assetClassNames.GetValueOrDefault(category.AssetClassId, string.Empty);
				var bucket = SymbolCategoryClassifier.Classify(category.Name, assetClassName);

				if (existing.TryGetValue(category.Id, out var entity))
				{
					entity.Name = category.Name;
					entity.AssetClassId = category.AssetClassId;
					entity.AssetClassName = assetClassName;
					entity.Bucket = bucket;
					entity.ChangedAt = now;
				}
				else
				{
					context.SymbolCategories.Add(
						new Data.Entities.SymbolCategory
						{
							Id = Guid.NewGuid(),
							SymbolCategoryId = category.Id,
							Name = category.Name,
							AssetClassId = category.AssetClassId,
							AssetClassName = assetClassName,
							Bucket = bucket,
							CreatedAt = now,
						}
					);
				}
			}

			await context.SaveChangesAsync(cancellationToken);

			var result = await ListCategoriesAsync(cancellationToken);
			_logger.LogInformation(
				"Refreshed {CategoryCount} symbol categor(y/ies) from cTrader: {Categories}.",
				result.Count,
				string.Join(", ", result.Select(c => $"{c.Name}[{c.AssetClassName}]={c.Bucket}"))
			);

			return result;
		}

		private static SymbolCategoryModel ToCategoryModel(Data.Entities.SymbolCategory entity) =>
			new(
				entity.Id,
				entity.SymbolCategoryId,
				entity.Name,
				entity.AssetClassId,
				entity.AssetClassName,
				entity.Bucket,
				entity.CreatedAt,
				entity.ChangedAt
			);

		private static SymbolMetadataModel ToMetadataModel(Data.Entities.SymbolMetadata entity) =>
			new(
				entity.Id,
				entity.SymbolId,
				entity.Digits,
				entity.PipPosition,
				entity.EnableShortSelling,
				entity.GuaranteedStopLoss,
				entity.SwapRollover3Days,
				entity.SwapLong,
				entity.SwapShort,
				entity.MaxVolume,
				entity.MinVolume,
				entity.StepVolume,
				entity.MaxExposure,
				entity.CommissionType,
				entity.SlDistance,
				entity.TpDistance,
				entity.GslDistance,
				entity.GslCharge,
				entity.DistanceSetIn,
				entity.MinCommissionType,
				entity.MinCommissionAsset,
				entity.RolloverCommission,
				entity.SkipRolloverDays,
				entity.ScheduleTimeZone,
				entity.TradingMode,
				entity.RolloverCommission3Days,
				entity.SwapCalculationType,
				entity.LotSize,
				entity.PreciseTradingCommissionRate,
				entity.PreciseMinCommission,
				entity.PnlConversionFeeRate,
				entity.LeverageId,
				entity.SwapPeriod,
				entity.SwapTime,
				entity.SkipSWAPPeriods,
				entity.ChargeSwapAtWeekends,
				entity.MeasurementUnits,
				entity.CreatedAt,
				entity.ChangedAt,
				entity.ScheduleIntervals.Select(i => new SymbolScheduleIntervalModel(i.StartSecond, i.EndSecond)).ToList(),
				entity
					.Holidays.Select(h => new SymbolHolidayModel(
						h.HolidayId,
						h.Name,
						h.Description,
						h.ScheduleTimeZone,
						h.HolidayDate,
						h.IsRecurring,
						h.StartSecond,
						h.EndSecond
					))
					.ToList()
			);
	}
}
