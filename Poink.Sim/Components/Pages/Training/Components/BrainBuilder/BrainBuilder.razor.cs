using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    public partial class BrainBuilder : ComponentBase
    {
        [Inject] private IRlTrainingService Service { get; set; } = default!;

        [Inject] private IRewardPolicyService RewardPolicyService { get; set; } = default!;

        [Inject] private IBrainService BrainService { get; set; } = default!;

        [Parameter] public EventCallback<string> OnBrainCreated { get; set; }


        [Parameter] public string BrainName { get; set; } = "";
        [Parameter] public EventCallback<string> BrainNameChanged { get; set; }

        [Parameter] public string StrategyName { get; set; } = "";
        [Parameter] public EventCallback<string> StrategyNameChanged { get; set; }

        [Parameter] public double LearningRate { get; set; }
        [Parameter] public EventCallback<double> LearningRateChanged { get; set; }

        private string? _lastBrainName;

        private IReadOnlyList<BrainOption> TrainableBrainOptions =>
            BrainService.Brains.Select(n => n.Name)
                .Concat(BrainService.DraftBrainNames)
                .Distinct()
                .Select(name => new BrainOption(name, true))
                .ToList();

        protected override void OnInitialized()
        {
            if (string.IsNullOrEmpty(BrainName) && TrainableBrainOptions.Count > 0)
                _ = BrainNameChanged.InvokeAsync(TrainableBrainOptions[0].Name);
            if (string.IsNullOrEmpty(StrategyName) && Service.StrategyNames.Count > 0)
                _ = StrategyNameChanged.InvokeAsync(Service.StrategyNames[0]);
        }

        protected override void OnParametersSet()
        {
            if (BrainName != _lastBrainName)
            {
                _lastBrainName = BrainName;
                if (!string.IsNullOrEmpty(BrainName) && BrainService.GetRecipe(BrainName) is { } recipe)
                    LoadRecipe(BrainName, recipe);
            }
        }


        private bool IsDraft => PersistedRecipeForCurrentName is not { } persisted || !RecipesEqual(persisted, CurrentRecipe);

        private BrainRecipe? PersistedRecipeForCurrentName =>
            BrainService.Brains.FirstOrDefault(b => b.Name == EffectiveBuildBrainName)?.Recipe;

        private bool BrainNameIsPersisted => BrainService.Brains.Any(b => b.Name == BrainName);

        private static bool RecipesEqual(BrainRecipe a, BrainRecipe b) =>
            a.ArchitectureKind == b.ArchitectureKind
            && a.ActionOutputEncodingKind == b.ActionOutputEncodingKind
            && a.DecisionDecoding == b.DecisionDecoding
            && a.DecisionTemperature == b.DecisionTemperature
            && a.CnnKernelSweepSource == b.CnnKernelSweepSource
            && Equals(a.EncoderSizes, b.EncoderSizes)
            && a.EncoderKinds.SequenceEqual(b.EncoderKinds)
            && a.HiddenSizes.SequenceEqual(b.HiddenSizes)
            && (a.ActionOutputKinds ?? []).SequenceEqual(b.ActionOutputKinds ?? [])
            && (a.EnabledDirections ?? ComposedPolicyBrain.AllDirections).SequenceEqual(b.EnabledDirections ?? ComposedPolicyBrain.AllDirections);

        private bool _showBuildPanel;

        private void ToggleBuildPanel()
        {
            _showBuildPanel = !_showBuildPanel;
            if (_showBuildPanel)
                _buildBrainStatus = null;
        }

        private bool _showDeleteConfirm;
        private string? _deleteStatus;

        private void CancelDeleteBrain() => _showDeleteConfirm = false;


        private void ConfirmDeleteBrain()
        {
            _showDeleteConfirm = false;
            var toDelete = BrainService.Brains.FirstOrDefault(b => b.Name == BrainName);
            if (toDelete is null)
            {
                _deleteStatus = "brain no longer exists";
                return;
            }

            if (BrainService.DeleteNetwork(toDelete.Id))
            {
                _deleteStatus = $"deleted '{toDelete.Name}'";
                _ = BrainNameChanged.InvokeAsync(TrainableBrainOptions.FirstOrDefault()?.Name ?? "");
            }
            else
            {
                _deleteStatus = BrainService.Status;
            }
        }

        private Task OnBrainNameChanged(string name) => BrainNameChanged.InvokeAsync(name);

        private Task OnStrategyChanged(ChangeEventArgs e) => StrategyNameChanged.InvokeAsync(e.Value?.ToString() ?? "");

        private void OnRewardPolicyChanged(ChangeEventArgs e) => RewardPolicyService.SelectPolicy(e.Value?.ToString() ?? "");

        private Task OnLearningRateChanged(ChangeEventArgs e) =>
            double.TryParse(e.Value?.ToString(), out var rate) ? LearningRateChanged.InvokeAsync(rate) : Task.CompletedTask;


        private void LoadRecipe(string name, BrainRecipe recipe)
        {
            _buildBrainName = name;
            _buildBrainNameAuto = false;

            _includeBarWindow = recipe.EncoderKinds.Contains(EncoderKind.BarWindow);
            _includeVolumeProfile = recipe.EncoderKinds.Contains(EncoderKind.VolumeProfile);
            _includeTimeProfile = recipe.EncoderKinds.Contains(EncoderKind.TimeProfile);
            _includeSessionClock = recipe.EncoderKinds.Contains(EncoderKind.SessionClock);
            _includeSpreadVolatility = recipe.EncoderKinds.Contains(EncoderKind.SpreadVolatility);

            var sizes = recipe.EncoderSizes ?? new EncoderSizeOptions();
            _barWindowNormalization = sizes.BarWindowNormalization;
            _volumeProfileNormalization = sizes.VolumeProfileNormalization;
            _timeProfileNormalization = sizes.TimeProfileNormalization;
            _volumeProfileSplitUpDown = sizes.VolumeProfileSplitUpDown;
            _timeProfileSplitUpDown = sizes.TimeProfileSplitUpDown;
            _barWindowSize = sizes.BarWindowSize;
            _volumeProfileBucketCount = sizes.VolumeProfileBucketCount;
            _timeProfileBucketCount = sizes.TimeProfileBucketCount;
            _volumeProfileLookbackBars = sizes.VolumeProfileLookbackBars;
            _timeProfileLookbackBars = sizes.TimeProfileLookbackBars;
            _spreadVolatilityAtrPeriod = sizes.SpreadVolatilityAtrPeriod;
            _barWindowZScoreSubWindowSize = sizes.BarWindowZScoreSubWindowSize;
            _volumeProfileZScoreSubWindowSize = sizes.VolumeProfileZScoreSubWindowSize;
            _includeTraderStateOutcomes = sizes.TraderStateTradeOutcomeListLength > 0;
            _includeTraderStatePositionLevel = sizes.TraderStatePositionLevelListLength > 0;
            _includeTraderStateDirection = sizes.TraderStateDirectionListLength > 0;
            _traderStateTradeOutcomeListLength = sizes.TraderStateTradeOutcomeListLength > 0 ? sizes.TraderStateTradeOutcomeListLength : _traderStateTradeOutcomeListLength;
            _traderStatePositionLevelListLength = sizes.TraderStatePositionLevelListLength > 0 ? sizes.TraderStatePositionLevelListLength : _traderStatePositionLevelListLength;
            _traderStateDirectionListLength = sizes.TraderStateDirectionListLength > 0 ? sizes.TraderStateDirectionListLength : _traderStateDirectionListLength;

            _traderStateIncludePosition = sizes.TraderStateIncludePosition;
            _traderStateIncludePositionPrice = sizes.TraderStateIncludePositionPrice;
            _traderStateIncludePositionTicks = sizes.TraderStateIncludePositionTicks;
            _traderStateIncludePositionDistance = sizes.TraderStateIncludePositionDistance;
            _traderStateIncludeCurrentPrice = sizes.TraderStateIncludeCurrentPrice;
            _traderStateIncludeCash = sizes.TraderStateIncludeCash;
            _traderStateIncludeRealized = sizes.TraderStateIncludeRealized;
            _traderStateIncludeUnrealized = sizes.TraderStateIncludeUnrealized;
            _traderStateIncludeEquity = sizes.TraderStateIncludeEquity;
            _traderStateIncludeMargin = sizes.TraderStateIncludeMargin;
            _traderStateIncludeMarginLevel = sizes.TraderStateIncludeMarginLevel;
            _traderStateIncludePositionCount = sizes.TraderStateIncludePositionCount;

            _xgridRankTopTotal = sizes.XGridRankTopTotal;
            _xgridRankTopUp = sizes.XGridRankTopUp;
            _xgridRankTopDown = sizes.XGridRankTopDown;
            _xgridAtPoc = sizes.XGridAtPoc;
            _xgridAtVah = sizes.XGridAtVah;
            _xgridAtVal = sizes.XGridAtVal;
            _xgridAtVwap = sizes.XGridAtVwap;
            _xgridAbovePoc = sizes.XGridAbovePoc;
            _xgridAboveVah = sizes.XGridAboveVah;
            _xgridAboveVal = sizes.XGridAboveVal;
            _xgridAboveVwap = sizes.XGridAboveVwap;
            _xgridBelowPoc = sizes.XGridBelowPoc;
            _xgridBelowVah = sizes.XGridBelowVah;
            _xgridBelowVal = sizes.XGridBelowVal;
            _xgridBelowVwap = sizes.XGridBelowVwap;
            _xgridTopPercent = sizes.XGridTopPercent;
            _xgridColCount = sizes.XGridColCount;
            _xgridRowCount = sizes.XGridRowCount;
            _xgridPipSize = sizes.XGridPipSize;
            _xgridWindowBars = sizes.XGridWindowBars;

            _architectureKind = recipe.ArchitectureKind;
            switch (recipe.ArchitectureKind)
            {
                case ArchitectureKind.Cnn:
                    _cnnKernelSize = recipe.HiddenSizes.Count > 0 ? recipe.HiddenSizes[0] : 3;
                    _cnnChannels = recipe.HiddenSizes.Count > 1 ? recipe.HiddenSizes.Skip(1).ToList() : [16, 32];
                    _cnnKernelSweepSource = recipe.CnnKernelSweepSource;
                    break;
                case ArchitectureKind.Lstm:
                    _lstmHiddenSize = recipe.HiddenSizes.Count > 0 ? recipe.HiddenSizes[0] : 64;
                    _lstmNumLayers = recipe.HiddenSizes.Count > 1 ? recipe.HiddenSizes[1] : 2;
                    break;
                default:
                    _mlpHiddenSizes = recipe.HiddenSizes.Count > 0 ? recipe.HiddenSizes.ToList() : [64];
                    break;
            }

            var actionKinds = recipe.ActionOutputKinds ?? [];
            _includeVolumeOutput = actionKinds.Contains(ActionOutputKind.Volume);
            _includeStopLossOutput = actionKinds.Contains(ActionOutputKind.StopLoss);
            _includeTakeProfitOutput = actionKinds.Contains(ActionOutputKind.TakeProfit);

            var directions = recipe.EnabledDirections ?? ComposedPolicyBrain.AllDirections;
            _includeHold = directions.Contains(SimulationAction.Hold);
            _includeEnterLong = directions.Contains(SimulationAction.EnterLong);
            _includeEnterShort = directions.Contains(SimulationAction.EnterShort);
            _includeClose = directions.Contains(SimulationAction.Close);

            _decisionDecoding = recipe.DecisionDecoding;
            _decisionTemperature = recipe.DecisionTemperature;

            _buildBrainStatus = null;
        }

        private string _buildBrainName = "";

        private bool _buildBrainNameAuto = true;
        private bool _includeBarWindow = true;
        private bool _includeVolumeProfile;
        private bool _includeTimeProfile;
        private bool _includeSessionClock;
        private bool _includeSpreadVolatility;
        private bool _includeTraderState;

        private bool _timeSeriesInputsHidden = true;

        private NormalizationKind _barWindowNormalization = NormalizationKind.Normalized;
        private NormalizationKind _volumeProfileNormalization = NormalizationKind.Normalized;
        private NormalizationKind _timeProfileNormalization = NormalizationKind.Normalized;

        private bool _volumeProfileSplitUpDown;
        private bool _timeProfileSplitUpDown;

        private int _barWindowSize = 20;
        private int _volumeProfileBucketCount = 20;
        private int _timeProfileBucketCount = 20;
        private int _volumeProfileLookbackBars = 50;
        private int _timeProfileLookbackBars = 50;
        private int _spreadVolatilityAtrPeriod = 14;
        private int _barWindowZScoreSubWindowSize = 5;
        private int _volumeProfileZScoreSubWindowSize = 5;

        private bool _includeTraderStateOutcomes;
        private bool _includeTraderStatePositionLevel;
        private bool _includeTraderStateDirection;

        private int _traderStateTradeOutcomeListLength = 5;
        private int _traderStatePositionLevelListLength = 5;
        private int _traderStateDirectionListLength = 5;

        private bool AllTraderStateListsSelected
        {
            get => _includeTraderStateOutcomes && _includeTraderStatePositionLevel && _includeTraderStateDirection;
            set
            {
                _includeTraderStateOutcomes = value;
                _includeTraderStatePositionLevel = value;
                _includeTraderStateDirection = value;
            }
        }

        private bool _traderStateIncludePosition = true;
        private bool _traderStateIncludePositionPrice = true;
        private bool _traderStateIncludePositionTicks = true;
        private bool _traderStateIncludePositionDistance = true;
        private bool _traderStateIncludeCurrentPrice = true;
        private bool _traderStateIncludeCash = true;
        private bool _traderStateIncludeRealized = true;
        private bool _traderStateIncludeUnrealized = true;
        private bool _traderStateIncludeEquity = true;
        private bool _traderStateIncludeMargin = true;
        private bool _traderStateIncludeMarginLevel = true;
        private bool _traderStateIncludePositionCount = true;

        private sealed record TraderStateFieldRow(string Label, string Tooltip, Func<bool> Get, Action<bool> Set);

        // (Label, Tooltip, Get, Set) per base field -- drives the checkbox row in the Trader
        // State card from one list instead of 12 hand-written near-duplicate blocks, same
        // "computed property returns a fresh collection each access" convention as XGridSignalRows.
        private IReadOnlyList<TraderStateFieldRow> TraderStateFieldRows => new List<TraderStateFieldRow>
        {
            new("Position", "Direction of the open position: 1 long, -1 short, 0 flat",
                () => _traderStateIncludePosition, v => _traderStateIncludePosition = v),
            new("Position Price", "Entry price of the open position, 0 if flat",
                () => _traderStateIncludePositionPrice, v => _traderStateIncludePositionPrice = v),
            new("Position Ticks", "Ticks the open position has been held, 0 if flat",
                () => _traderStateIncludePositionTicks, v => _traderStateIncludePositionTicks = v),
            new("Position Distance", "Cumulative price distance traveled since the position opened, 0 if flat",
                () => _traderStateIncludePositionDistance, v => _traderStateIncludePositionDistance = v),
            new("Current Price", "Current market price",
                () => _traderStateIncludeCurrentPrice, v => _traderStateIncludeCurrentPrice = v),
            new("Cash", "Available cash balance",
                () => _traderStateIncludeCash, v => _traderStateIncludeCash = v),
            new("Realized", "Realized profit/loss booked so far",
                () => _traderStateIncludeRealized, v => _traderStateIncludeRealized = v),
            new("Unrealized", "Unrealized profit/loss on the open position",
                () => _traderStateIncludeUnrealized, v => _traderStateIncludeUnrealized = v),
            new("Equity", "Cash plus unrealized profit/loss",
                () => _traderStateIncludeEquity, v => _traderStateIncludeEquity = v),
            new("Margin", "Margin currently used by the open position",
                () => _traderStateIncludeMargin, v => _traderStateIncludeMargin = v),
            new("Margin Level", "Equity as a percent of margin used",
                () => _traderStateIncludeMarginLevel, v => _traderStateIncludeMarginLevel = v),
            new("Position Count", "1 if a position is open, 0 if flat",
                () => _traderStateIncludePositionCount, v => _traderStateIncludePositionCount = v),
        };

        private const int TotalTraderStateFieldCount = 12;

        private int SelectedTraderStateFieldCount => TraderStateFieldRows.Count(r => r.Get());

        private bool AllTraderStateBaseFieldsSelected
        {
            get => SelectedTraderStateFieldCount == TotalTraderStateFieldCount;
            set
            {
                foreach (var row in TraderStateFieldRows)
                    row.Set(value);
            }
        }

        private bool _xgridRankTopTotal;
        private bool _xgridRankTopUp;
        private bool _xgridRankTopDown;
        private bool _xgridAtPoc;
        private bool _xgridAtVah;
        private bool _xgridAtVal;
        private bool _xgridAtVwap;
        private bool _xgridAbovePoc;
        private bool _xgridAboveVah;
        private bool _xgridAboveVal;
        private bool _xgridAboveVwap;
        private bool _xgridBelowPoc;
        private bool _xgridBelowVah;
        private bool _xgridBelowVal;
        private bool _xgridBelowVwap;

        private double _xgridTopPercent = 10;
        private int _xgridColCount = 20;
        private int _xgridRowCount = 20;
        private double _xgridPipSize = 0.01;
        private int _xgridWindowBars = 60;

        private const int TotalXGridSignalCount = 15;

        private sealed record XGridSignalRow(string Group, string Label, string Tooltip, Func<bool> Get, Action<bool> Set);

        // (Group, Label, Tooltip, Get, Set) per signal -- drives the checkbox rows in the
        // XGrid Signals card from one list instead of 15 hand-written near-duplicate blocks.
        // Get/Set close over this component's own _xgrid* fields, same "computed property
        // returns a fresh collection each access" convention as EnabledDirectionLabels.
        private IReadOnlyList<XGridSignalRow> XGridSignalRows => new List<XGridSignalRow>
        {
            new("Rank", "Top % Total", "Current cell's total tick count ranks in the top % of every nonzero cell on the grid",
                () => _xgridRankTopTotal, v => _xgridRankTopTotal = v),
            new("Rank", "Top % Up", "Current cell's up-tick count ranks in the top % of every nonzero cell (Up metric)",
                () => _xgridRankTopUp, v => _xgridRankTopUp = v),
            new("Rank", "Top % Down", "Current cell's down-tick count ranks in the top % of every nonzero cell (Down metric)",
                () => _xgridRankTopDown, v => _xgridRankTopDown = v),
            new("At", "At POC", "Current price's row matches the Point of Control's row",
                () => _xgridAtPoc, v => _xgridAtPoc = v),
            new("At", "At VAH", "Current price's row matches the Value Area High's row",
                () => _xgridAtVah, v => _xgridAtVah = v),
            new("At", "At VAL", "Current price's row matches the Value Area Low's row",
                () => _xgridAtVal, v => _xgridAtVal = v),
            new("At", "At VWAP", "Current price's row matches the VWAP's row",
                () => _xgridAtVwap, v => _xgridAtVwap = v),
            new("Above", "Above POC", "Current price is above the Point of Control's price",
                () => _xgridAbovePoc, v => _xgridAbovePoc = v),
            new("Above", "Above VAH", "Current price is above the Value Area High's price",
                () => _xgridAboveVah, v => _xgridAboveVah = v),
            new("Above", "Above VAL", "Current price is above the Value Area Low's price",
                () => _xgridAboveVal, v => _xgridAboveVal = v),
            new("Above", "Above VWAP", "Current price is above the VWAP's price",
                () => _xgridAboveVwap, v => _xgridAboveVwap = v),
            new("Below", "Below POC", "Current price is below the Point of Control's price",
                () => _xgridBelowPoc, v => _xgridBelowPoc = v),
            new("Below", "Below VAH", "Current price is below the Value Area High's price",
                () => _xgridBelowVah, v => _xgridBelowVah = v),
            new("Below", "Below VAL", "Current price is below the Value Area Low's price",
                () => _xgridBelowVal, v => _xgridBelowVal = v),
            new("Below", "Below VWAP", "Current price is below the VWAP's price",
                () => _xgridBelowVwap, v => _xgridBelowVwap = v),
        };

        private IReadOnlyList<IGrouping<string, XGridSignalRow>> XGridSignalGroups => XGridSignalRows.GroupBy(r => r.Group).ToList();

        private int SelectedXGridSignalCount => XGridSignalRows.Count(r => r.Get());

        private bool AnyXGridSignalSelected => SelectedXGridSignalCount > 0;

        private bool AllXGridSignalsSelected
        {
            get => SelectedXGridSignalCount == TotalXGridSignalCount;
            set
            {
                foreach (var row in XGridSignalRows)
                    row.Set(value);
            }
        }

        private ArchitectureKind _architectureKind = ArchitectureKind.Mlp;

        private List<int> _mlpHiddenSizes = [64];
        private int _lstmHiddenSize = 64;
        private int _lstmNumLayers = 2;

        private int _cnnKernelSize = 3;
        private List<int> _cnnChannels = [16, 32];
        private CnnKernelSweepSource _cnnKernelSweepSource = CnnKernelSweepSource.BarWindow;

        private bool _includeVolumeOutput;
        private bool _includeStopLossOutput;
        private bool _includeTakeProfitOutput;

        private bool _includeHold = true;
        private bool _includeEnterLong = true;
        private bool _includeEnterShort = true;
        private bool _includeClose = true;

        private DecisionDecodingKind _decisionDecoding = DecisionDecodingKind.Softmax;

        private double _decisionTemperature = 1.0;
        private string? _buildBrainStatus;

        private void OnArchitectureKindChanged(ChangeEventArgs e) =>
            _architectureKind = Enum.Parse<ArchitectureKind>(e.Value?.ToString() ?? nameof(ArchitectureKind.Mlp));

        private void OnCnnKernelSweepSourceChanged(ChangeEventArgs e) =>
            _cnnKernelSweepSource = Enum.Parse<CnnKernelSweepSource>(e.Value?.ToString() ?? nameof(CnnKernelSweepSource.BarWindow));

        private void OnDecisionDecodingKindChanged(ChangeEventArgs e) =>
            _decisionDecoding = Enum.Parse<DecisionDecodingKind>(e.Value?.ToString() ?? nameof(DecisionDecodingKind.Softmax));

        private void OnBarWindowNormalizationChanged(ChangeEventArgs e) =>
            _barWindowNormalization = Enum.Parse<NormalizationKind>(e.Value?.ToString() ?? nameof(NormalizationKind.Normalized));

        private void OnVolumeProfileNormalizationChanged(ChangeEventArgs e) =>
            _volumeProfileNormalization = Enum.Parse<NormalizationKind>(e.Value?.ToString() ?? nameof(NormalizationKind.Normalized));

        private void OnTimeProfileNormalizationChanged(ChangeEventArgs e) =>
            _timeProfileNormalization = Enum.Parse<NormalizationKind>(e.Value?.ToString() ?? nameof(NormalizationKind.Normalized));

        private IReadOnlyList<int> EffectiveMlpHiddenSizes => _mlpHiddenSizes.Where(n => n > 0).ToList();

        private void AddHiddenLayer() => _mlpHiddenSizes.Add(Math.Max(1, _mlpHiddenSizes[^1] / 2));

        private void AddHiddenLayerOnTop() => _mlpHiddenSizes.Insert(0, _mlpHiddenSizes[0] * 2);

        private void RemoveHiddenLayer(int index)
        {
            if (_mlpHiddenSizes.Count <= 1)
                return;
            _mlpHiddenSizes.RemoveAt(index);
        }

        private bool CanRemoveHiddenLayer => _mlpHiddenSizes.Count > 1;

        private void OnHiddenLayerSizeChanged(int index, ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int size))
                _mlpHiddenSizes[index] = size;
        }

        private IReadOnlyList<int> EffectiveCnnChannels => _cnnChannels.Where(n => n > 0).ToList();

        private void AddCnnChannelLayer() => _cnnChannels.Add(_cnnChannels[^1] * 2);

        private void RemoveCnnChannelLayer(int index)
        {
            if (_cnnChannels.Count <= 1)
                return;
            _cnnChannels.RemoveAt(index);
        }

        private bool CanRemoveCnnChannelLayer => _cnnChannels.Count > 1;

        private void OnCnnChannelSizeChanged(int index, ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int size))
                _cnnChannels[index] = size;
        }


        private bool CnnSweepSourceIncluded =>
            _cnnKernelSweepSource == CnnKernelSweepSource.VolumeProfile ? _includeVolumeProfile : _includeBarWindow;

        private bool CanCreateBrain =>
            !string.IsNullOrWhiteSpace(EffectiveBuildBrainName)
            && (_includeBarWindow || _includeVolumeProfile || _includeTimeProfile || _includeSessionClock || _includeSpreadVolatility || _includeTraderState || AnyXGridSignalSelected)
            && (_includeHold || _includeEnterLong || _includeEnterShort || _includeClose)
            && (_architectureKind != ArchitectureKind.Lstm || _includeBarWindow)
            && (_architectureKind != ArchitectureKind.Cnn || (CnnSweepSourceIncluded && _cnnKernelSize >= 1 && EffectiveCnnChannels.Count > 0))
            && (!_includeBarWindow || _barWindowSize >= 1)
            && (!_includeVolumeProfile || (_volumeProfileBucketCount >= 1 && _volumeProfileLookbackBars >= 1))
            && (!_includeTimeProfile || (_timeProfileBucketCount >= 1 && _timeProfileLookbackBars >= 1))
            && (!_includeSpreadVolatility || _spreadVolatilityAtrPeriod >= 1)
            && (_barWindowNormalization != NormalizationKind.LocalZScore || _barWindowZScoreSubWindowSize >= 1)
            && (_volumeProfileNormalization != NormalizationKind.LocalZScore || _volumeProfileZScoreSubWindowSize >= 1)
            && (!_includeTraderStateOutcomes || _traderStateTradeOutcomeListLength >= 1)
            && (!_includeTraderStatePositionLevel || _traderStatePositionLevelListLength >= 1)
            && (!_includeTraderStateDirection || _traderStateDirectionListLength >= 1)
            && (!AnyXGridSignalSelected || (_xgridColCount >= 1 && _xgridRowCount >= 1 && _xgridPipSize > 0 && _xgridWindowBars >= 1 && _xgridTopPercent is >= 0 and <= 100))
            && _decisionTemperature > 0;

        private IReadOnlyList<EncoderKind> SelectedEncoderKinds
        {
            get
            {
                var kinds = new List<EncoderKind>();
                if (_includeBarWindow)
                    kinds.Add(EncoderKind.BarWindow);
                if (_includeVolumeProfile)
                    kinds.Add(EncoderKind.VolumeProfile);
                if (_includeTimeProfile)
                    kinds.Add(EncoderKind.TimeProfile);
                if (_includeSessionClock)
                    kinds.Add(EncoderKind.SessionClock);
                if (_includeSpreadVolatility)
                    kinds.Add(EncoderKind.SpreadVolatility);
                if (_includeTraderState)
                    kinds.Add(EncoderKind.TraderState);
                if (AnyXGridSignalSelected)
                    kinds.Add(EncoderKind.XGrid);
                return kinds;
            }
        }

        private EncoderSizeOptions CurrentEncoderSizeOptions => new(
            BarWindowSize: _barWindowSize,
            VolumeProfileBucketCount: _volumeProfileBucketCount,
            TimeProfileBucketCount: _timeProfileBucketCount,
            VolumeProfileLookbackBars: _volumeProfileLookbackBars,
            TimeProfileLookbackBars: _timeProfileLookbackBars,
            BarWindowNormalization: _barWindowNormalization,
            VolumeProfileNormalization: _volumeProfileNormalization,
            TimeProfileNormalization: _timeProfileNormalization,
            SpreadVolatilityAtrPeriod: _spreadVolatilityAtrPeriod,
            VolumeProfileSplitUpDown: _volumeProfileSplitUpDown,
            TimeProfileSplitUpDown: _timeProfileSplitUpDown,
            BarWindowZScoreSubWindowSize: _barWindowZScoreSubWindowSize,
            VolumeProfileZScoreSubWindowSize: _volumeProfileZScoreSubWindowSize,
            TraderStateTradeOutcomeListLength: _includeTraderStateOutcomes ? _traderStateTradeOutcomeListLength : 0,
            TraderStatePositionLevelListLength: _includeTraderStatePositionLevel ? _traderStatePositionLevelListLength : 0,
            TraderStateDirectionListLength: _includeTraderStateDirection ? _traderStateDirectionListLength : 0,
            TraderStateIncludePosition: _traderStateIncludePosition,
            TraderStateIncludePositionPrice: _traderStateIncludePositionPrice,
            TraderStateIncludePositionTicks: _traderStateIncludePositionTicks,
            TraderStateIncludePositionDistance: _traderStateIncludePositionDistance,
            TraderStateIncludeCurrentPrice: _traderStateIncludeCurrentPrice,
            TraderStateIncludeCash: _traderStateIncludeCash,
            TraderStateIncludeRealized: _traderStateIncludeRealized,
            TraderStateIncludeUnrealized: _traderStateIncludeUnrealized,
            TraderStateIncludeEquity: _traderStateIncludeEquity,
            TraderStateIncludeMargin: _traderStateIncludeMargin,
            TraderStateIncludeMarginLevel: _traderStateIncludeMarginLevel,
            TraderStateIncludePositionCount: _traderStateIncludePositionCount,
            XGridRankTopTotal: _xgridRankTopTotal,
            XGridRankTopUp: _xgridRankTopUp,
            XGridRankTopDown: _xgridRankTopDown,
            XGridAtPoc: _xgridAtPoc,
            XGridAtVah: _xgridAtVah,
            XGridAtVal: _xgridAtVal,
            XGridAtVwap: _xgridAtVwap,
            XGridAbovePoc: _xgridAbovePoc,
            XGridAboveVah: _xgridAboveVah,
            XGridAboveVal: _xgridAboveVal,
            XGridAboveVwap: _xgridAboveVwap,
            XGridBelowPoc: _xgridBelowPoc,
            XGridBelowVah: _xgridBelowVah,
            XGridBelowVal: _xgridBelowVal,
            XGridBelowVwap: _xgridBelowVwap,
            XGridTopPercent: _xgridTopPercent,
            XGridColCount: _xgridColCount,
            XGridRowCount: _xgridRowCount,
            XGridPipSize: _xgridPipSize,
            XGridWindowBars: _xgridWindowBars);

        private string VolumeProfileBucketsTooltip =>
            (_volumeProfileNormalization == NormalizationKind.Raw, _volumeProfileSplitUpDown) switch
            {
                (true, true) => "How many histogram buckets Volume Profile rebins its merged bars into -- each bucket feeds price, up-volume, down-volume, and their diff under Raw+Split, so its input count is this times 4 plus 3 summary values",
                (true, false) => "How many histogram buckets Volume Profile rebins its merged bars into -- each bucket feeds price and volume under Raw, so its input count is this times 2 plus 3 summary values",
                (false, true) => "How many histogram buckets Volume Profile rebins its merged bars into -- each bucket feeds up-volume, down-volume, and their diff under Split, so its input count is this times 3 plus 3 summary values",
                (false, false) => "How many histogram buckets Volume Profile rebins its merged bars into -- its input count is this plus 3 summary values",
            };

        private string TimeProfileBucketsTooltip =>
            (_timeProfileNormalization == NormalizationKind.Raw, _timeProfileSplitUpDown) switch
            {
                (true, true) => "How many histogram buckets Time Profile rebins its merged bars into -- each bucket feeds price, up-seconds, down-seconds, and their diff under Raw+Split, so its input count is this times 4 plus 3 summary values",
                (true, false) => "How many histogram buckets Time Profile rebins its merged bars into -- each bucket feeds price and seconds under Raw, so its input count is this times 2 plus 3 summary values",
                (false, true) => "How many histogram buckets Time Profile rebins its merged bars into -- each bucket feeds up-seconds, down-seconds, and their diff under Split, so its input count is this times 3 plus 3 summary values",
                (false, false) => "How many histogram buckets Time Profile rebins its merged bars into -- its input count is this plus 3 summary values",
            };

        private int TotalInputCount =>
            SelectedEncoderKinds.Sum(k => BrainCatalog.CreateEncoder(k, CurrentEncoderSizeOptions).FeatureCount);

        private int FeatureCountFor(EncoderKind kind) => BrainCatalog.CreateEncoder(kind, CurrentEncoderSizeOptions).FeatureCount;

        private int BarWindowFeatureCount => FeatureCountFor(EncoderKind.BarWindow);
        private int VolumeProfileFeatureCount => FeatureCountFor(EncoderKind.VolumeProfile);
        private int TimeProfileFeatureCount => FeatureCountFor(EncoderKind.TimeProfile);
        private int SessionClockFeatureCount => FeatureCountFor(EncoderKind.SessionClock);
        private int SpreadVolatilityFeatureCount => FeatureCountFor(EncoderKind.SpreadVolatility);
        private int TraderStateFeatureCount => FeatureCountFor(EncoderKind.TraderState);

        private int TimeSeriesInputsFeatureCount =>
            (_includeBarWindow ? BarWindowFeatureCount : 0)
            + (_includeVolumeProfile ? VolumeProfileFeatureCount : 0)
            + (_includeTimeProfile ? TimeProfileFeatureCount : 0)
            + (_includeSessionClock ? SessionClockFeatureCount : 0)
            + (_includeSpreadVolatility ? SpreadVolatilityFeatureCount : 0);

        private int? FirstHiddenLayerSize => _architectureKind switch
        {
            ArchitectureKind.Mlp => EffectiveMlpHiddenSizes.Count > 0 ? EffectiveMlpHiddenSizes[0] : null,
            ArchitectureKind.Cnn => EffectiveCnnChannels.Count > 0 ? EffectiveCnnChannels[0] : null,
            _ => _lstmHiddenSize,
        };

        private bool FirstLayerTooSmall => FirstHiddenLayerSize is int size && size < TotalInputCount;

        private const int TotalActionOptionCount = 7;

        private int SelectedActionCount =>
            DirectionCount
            + (_includeVolumeOutput ? 1 : 0)
            + (_includeStopLossOutput ? 1 : 0)
            + (_includeTakeProfitOutput ? 1 : 0);

        private bool AllActionsSelected
        {
            get => SelectedActionCount == TotalActionOptionCount;
            set
            {
                _includeHold = value;
                _includeEnterLong = value;
                _includeEnterShort = value;
                _includeClose = value;
                _includeVolumeOutput = value;
                _includeStopLossOutput = value;
                _includeTakeProfitOutput = value;
            }
        }

        private IReadOnlyList<string> EnabledDirectionLabels
        {
            get
            {
                var labels = new List<string>();
                if (_includeHold)
                    labels.Add("Hold");
                if (_includeEnterLong)
                    labels.Add("Enter Long");
                if (_includeEnterShort)
                    labels.Add("Enter Short");
                if (_includeClose)
                    labels.Add("Close");
                return labels;
            }
        }

        private int DirectionCount => EnabledDirectionLabels.Count;

        private int VolumeOutputWidth =>
            BrainCatalog.CreateActionOutputHead(ActionOutputKind.Volume, ActionOutputEncodingKind.DiscretizedLevels).OutputWidth;

        private int StopLossOutputWidth =>
            BrainCatalog.CreateActionOutputHead(ActionOutputKind.StopLoss, ActionOutputEncodingKind.DiscretizedLevels).OutputWidth;

        private int TakeProfitOutputWidth =>
            BrainCatalog.CreateActionOutputHead(ActionOutputKind.TakeProfit, ActionOutputEncodingKind.DiscretizedLevels).OutputWidth;

        private int TotalOutputWidth =>
            DirectionCount
            + (_includeVolumeOutput ? VolumeOutputWidth : 0)
            + (_includeStopLossOutput ? StopLossOutputWidth : 0)
            + (_includeTakeProfitOutput ? TakeProfitOutputWidth : 0);

        private string DecodingLabel => _decisionDecoding switch
        {
            DecisionDecodingKind.HardMax => "hardmax",
            _ when _decisionTemperature != 1.0 => $"softmax @ T={_decisionTemperature:0.##}",
            _ => "softmax",
        };

        private string AutoBuildBrainName
        {
            get
            {
                string arch = _architectureKind switch
                {
                    ArchitectureKind.Mlp => "MLP",
                    ArchitectureKind.Lstm => "LSTM",
                    _ => _architectureKind.ToString().ToUpperInvariant(),
                };

                string layers = _architectureKind switch
                {
                    ArchitectureKind.Mlp => string.Join("-", EffectiveMlpHiddenSizes.Select(n => $"L{n}")),
                    ArchitectureKind.Cnn => $"K{_cnnKernelSize}-" + string.Join("-", EffectiveCnnChannels.Select(n => $"C{n}"))
                        + (_cnnKernelSweepSource == CnnKernelSweepSource.VolumeProfile ? "-sweepVP" : ""),
                    _ => $"H{_lstmHiddenSize}-N{_lstmNumLayers}",
                };

                var dataParts = new List<string>();
                if (_includeBarWindow)
                    dataParts.Add(NormalizationTag(_barWindowNormalization, "BW"));
                if (_includeVolumeProfile)
                {
                    string vpTag = NormalizationTag(_volumeProfileNormalization, "VP");
                    dataParts.Add(_volumeProfileSplitUpDown ? $"{vpTag}-split" : vpTag);
                }
                if (_includeTimeProfile)
                {
                    string tpTag = _timeProfileNormalization == NormalizationKind.Raw ? "dTPR" : "dTP";
                    dataParts.Add(_timeProfileSplitUpDown ? $"{tpTag}-split" : tpTag);
                }
                if (_includeSessionClock)
                    dataParts.Add("dSC");
                if (_includeSpreadVolatility)
                    dataParts.Add("dSV");
                if (_includeTraderState)
                    dataParts.Add("dAS");
                if (AnyXGridSignalSelected)
                    dataParts.Add("dXG");
                string data = string.Join("-", dataParts);

                string name = string.IsNullOrEmpty(layers) ? $"{arch}_{data}" : $"{arch}_{layers}_{data}";

                var actionParts = new List<string>();
                if (!_includeHold)
                    actionParts.Add("noHOLD");
                if (!_includeEnterLong)
                    actionParts.Add("noLONG");
                if (!_includeEnterShort)
                    actionParts.Add("noSHORT");
                if (!_includeClose)
                    actionParts.Add("noCLOSE");
                if (_includeVolumeOutput)
                    actionParts.Add("aVOL");
                if (_includeStopLossOutput)
                    actionParts.Add("aSL");
                if (_includeTakeProfitOutput)
                    actionParts.Add("aTP");
                if (_decisionDecoding == DecisionDecodingKind.HardMax)
                    actionParts.Add("hardmax");
                else if (_decisionTemperature != 1.0)
                    actionParts.Add($"T{_decisionTemperature:0.##}");
                return actionParts.Count == 0 ? name : $"{name}_{string.Join("-", actionParts)}";
            }
        }

        private static string NormalizationTag(NormalizationKind kind, string setPrefix) => kind switch
        {
            NormalizationKind.Raw => $"d{setPrefix}R",
            NormalizationKind.LocalZScore => $"d{setPrefix}ZL",
            NormalizationKind.GlobalZScore => $"d{setPrefix}ZG",
            NormalizationKind.Binary => $"d{setPrefix}B",
            _ => $"d{setPrefix}",
        };

        private string EffectiveBuildBrainName => _buildBrainNameAuto ? AutoBuildBrainName : _buildBrainName;

        private void OnBuildBrainNameInput(ChangeEventArgs e)
        {
            _buildBrainNameAuto = false;
            _buildBrainName = e.Value?.ToString() ?? "";
        }

        private void OnToggleAutoBuildBrainName(ChangeEventArgs e)
        {
            _buildBrainNameAuto = e.Value is bool isAuto && isAuto;
            if (!_buildBrainNameAuto)
                _buildBrainName = AutoBuildBrainName;
        }

        private IReadOnlyList<int> CurrentHiddenSizes => _architectureKind switch
        {
            ArchitectureKind.Mlp => EffectiveMlpHiddenSizes,
            ArchitectureKind.Cnn => [_cnnKernelSize, .. EffectiveCnnChannels],
            _ => [_lstmHiddenSize, _lstmNumLayers],
        };

        private List<ActionOutputKind> SelectedActionOutputKinds
        {
            get
            {
                var kinds = new List<ActionOutputKind>();
                if (_includeVolumeOutput)
                    kinds.Add(ActionOutputKind.Volume);
                if (_includeStopLossOutput)
                    kinds.Add(ActionOutputKind.StopLoss);
                if (_includeTakeProfitOutput)
                    kinds.Add(ActionOutputKind.TakeProfit);
                return kinds;
            }
        }

        private List<SimulationAction> SelectedEnabledDirections
        {
            get
            {
                var directions = new List<SimulationAction>();
                if (_includeHold)
                    directions.Add(SimulationAction.Hold);
                if (_includeEnterLong)
                    directions.Add(SimulationAction.EnterLong);
                if (_includeEnterShort)
                    directions.Add(SimulationAction.EnterShort);
                if (_includeClose)
                    directions.Add(SimulationAction.Close);
                return directions;
            }
        }

        private BrainRecipe CurrentRecipe => new(
            SelectedEncoderKinds.ToList(), _architectureKind, CurrentHiddenSizes.ToList(),
            SelectedActionOutputKinds.Count > 0 ? SelectedActionOutputKinds : null, ActionOutputEncodingKind.DiscretizedLevels,
            SelectedEnabledDirections.Count > 0 ? SelectedEnabledDirections : null, CurrentEncoderSizeOptions,
            _decisionDecoding, _decisionTemperature, _cnnKernelSweepSource);


        public BrainRecipe CurrentConfigRecipe => CurrentRecipe;

        private void SaveBrain()
        {
            var name = EffectiveBuildBrainName;

            if (BrainService.TryBuildBrain(
                    name, SelectedEncoderKinds, _architectureKind, CurrentHiddenSizes,
                    SelectedActionOutputKinds, SelectedEnabledDirections, CurrentEncoderSizeOptions,
                    _decisionDecoding, _decisionTemperature, _cnnKernelSweepSource))
            {
                _buildBrainStatus = $"saved '{name}' to storage";
                _ = OnBrainCreated.InvokeAsync(name);
            }
            else
            {
                _buildBrainStatus = BrainService.Status;
            }
        }

        private bool TryDraftCurrentConfig()
        {
            if (!CanCreateBrain)
            {
                _buildBrainStatus = "fix the build panel before using this config";
                return false;
            }

            if (!BrainService.TryBuildDraftBrain(
                    EffectiveBuildBrainName, SelectedEncoderKinds, _architectureKind, CurrentHiddenSizes,
                    SelectedActionOutputKinds, SelectedEnabledDirections, CurrentEncoderSizeOptions,
                    _decisionDecoding, _decisionTemperature, _cnnKernelSweepSource))
            {
                _buildBrainStatus = BrainService.Status;
                return false;
            }

            _buildBrainStatus = null;
            return true;
        }


        private void SetConfig() => TryDraftCurrentConfig();


        public string? PrepareBrainForTraining()
        {
            if (!IsDraft)
                return BrainName;
            return TryDraftCurrentConfig() ? EffectiveBuildBrainName : null;
        }
    }
}
