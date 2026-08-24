using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
	public partial class RewardChart : ComponentBase
	{
		private const double ViewWidth = 600;
		private const double ViewHeight = 160;
		private const double PadLeft = 8;
		private const double PadRight = 8;
		private const double PadTop = 10;
		private const double PadBottom = 10;

		private static readonly int[] CandleSizeOptions = { 1, 5, 10, 20, 50 };

		[Parameter]
		public IReadOnlyList<RlEpisodeMetric> Episodes { get; set; } = Array.Empty<RlEpisodeMetric>();

		[Parameter]
		public bool IsRunning { get; set; }

		private int _candleSize = 1;

		private (double Min, double Max) _rewardRange = (-1, 1);

		private sealed record ChartPoint(string X, string Y, int Episode, double Reward, bool IsLatest);

		private sealed record Candle(
			string BodyX,
			string BodyWidth,
			string BodyY,
			string BodyHeight,
			string WickX,
			string WickTopY,
			string WickBottomY,
			bool IsUp,
			int FirstEpisode,
			int LastEpisode,
			double Open,
			double High,
			double Low,
			double Close
		);

		private static double PlotWidth => ViewWidth - PadLeft - PadRight;
		private static double PlotHeight => ViewHeight - PadTop - PadBottom;

		protected override void OnParametersSet() => _rewardRange = ComputeRewardRange(Episodes);

		private static (double Min, double Max) ComputeRewardRange(IReadOnlyList<RlEpisodeMetric> episodes)
		{
			if (episodes.Count == 0)
				return (-1, 1);
			double min = episodes.Min(e => e.Reward);
			double max = episodes.Max(e => e.Reward);
			if (Math.Abs(max - min) < 1e-6)
			{
				min -= 1;
				max += 1;
			}
			return (min, max);
		}

		private (double Min, double Max) RewardRange => _rewardRange;

		private double MapX(int index) => MapXForSlot(index, Episodes.Count);

		private static double MapXForSlot(int index, int slotCount) =>
			slotCount <= 1 ? PadLeft + PlotWidth / 2 : PadLeft + index * (PlotWidth / (slotCount - 1));

		private double MapY(double reward)
		{
			var (min, max) = _rewardRange;
			double t = (reward - min) / (max - min);
			return PadTop + (1 - t) * PlotHeight;
		}

		private static string FormatCoord(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

		private string BaselineY => FormatCoord(MapY(0));

		private List<ChartPoint> Points =>
			Episodes
				.Select(
					(e, i) =>
						new ChartPoint(
							FormatCoord(MapX(i)),
							FormatCoord(MapY(e.Reward)),
							e.EpisodeNumber,
							e.Reward,
							i == Episodes.Count - 1
						)
				)
				.ToList();

		private string PolylinePoints => string.Join(" ", Points.Select(p => $"{p.X},{p.Y}"));

		private List<Candle> Candles
		{
			get
			{
				var candles = new List<Candle>();
				if (_candleSize <= 1 || Episodes.Count == 0)
					return candles;

				int bucketCount = (int)Math.Ceiling(Episodes.Count / (double)_candleSize);
				double slotWidth = bucketCount <= 1 ? PlotWidth : PlotWidth / (bucketCount - 1);

				double halfWidth = Math.Clamp(slotWidth * 0.32, 1.5, 12);

				for (int b = 0; b < bucketCount; b++)
				{
					int start = b * _candleSize;
					int count = Math.Min(_candleSize, Episodes.Count - start);

					double open = Episodes[start].Reward;
					double close = Episodes[start + count - 1].Reward;
					double high = double.NegativeInfinity;
					double low = double.PositiveInfinity;
					for (int i = start; i < start + count; i++)
					{
						high = Math.Max(high, Episodes[i].Reward);
						low = Math.Min(low, Episodes[i].Reward);
					}

					double centerX = MapXForSlot(b, bucketCount);
					double bodyTop = MapY(Math.Max(open, close));
					double bodyBottom = MapY(Math.Min(open, close));
					if (bodyBottom - bodyTop < 1.5)
					{
						double mid = (bodyTop + bodyBottom) / 2;
						bodyTop = mid - 0.75;
						bodyBottom = mid + 0.75;
					}

					candles.Add(
						new Candle(
							BodyX: FormatCoord(centerX - halfWidth),
							BodyWidth: FormatCoord(halfWidth * 2),
							BodyY: FormatCoord(bodyTop),
							BodyHeight: FormatCoord(bodyBottom - bodyTop),
							WickX: FormatCoord(centerX),
							WickTopY: FormatCoord(MapY(high)),
							WickBottomY: FormatCoord(MapY(low)),
							IsUp: close >= open,
							FirstEpisode: Episodes[start].EpisodeNumber,
							LastEpisode: Episodes[start + count - 1].EpisodeNumber,
							Open: open,
							High: high,
							Low: low,
							Close: close
						)
					);
				}
				return candles;
			}
		}

		private void OnCandleSizeChanged(ChangeEventArgs e)
		{
			if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
			{
				_candleSize = size;
			}
		}
	}
}
