using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    public partial class BrainBuilder : ComponentBase
    {
        [Inject] private RlTrainingService Service { get; set; } = default!;

        [Parameter] public bool Visible { get; set; }

        [Parameter] public EventCallback<string> OnBrainCreated { get; set; }

        private bool _wasVisible;

        protected override void OnParametersSet()
        {
            if (Visible && !_wasVisible) _buildBrainStatus = null;
            _wasVisible = Visible;
        }

        private string _buildBrainName = "";

        private bool _buildBrainNameAuto = true;
        private bool _includeBarWindow = true;
        private bool _includeVolumeProfile;
        private bool _includeTimeProfile;
        private bool _includeSessionClock;
        private bool _includeSpreadVolatility;

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

        private ArchitectureKind _architectureKind = ArchitectureKind.Mlp;

        private List<int> _mlpHiddenSizes = [64];
        private int _lstmHiddenSize = 64;
        private int _lstmNumLayers = 2;

        private int _cnnKernelSize = 3;
        private List<int> _cnnChannels = [16, 32];

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
            if (_mlpHiddenSizes.Count <= 1) return;
            _mlpHiddenSizes.RemoveAt(index);
        }

        private bool CanRemoveHiddenLayer => _mlpHiddenSizes.Count > 1;

        private void OnHiddenLayerSizeChanged(int index, ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int size)) _mlpHiddenSizes[index] = size;
        }

        private IReadOnlyList<int> EffectiveCnnChannels => _cnnChannels.Where(n => n > 0).ToList();

        private void AddCnnChannelLayer() => _cnnChannels.Add(_cnnChannels[^1] * 2);

        private void RemoveCnnChannelLayer(int index)
        {
            if (_cnnChannels.Count <= 1) return;
            _cnnChannels.RemoveAt(index);
        }

        private bool CanRemoveCnnChannelLayer => _cnnChannels.Count > 1;

        private void OnCnnChannelSizeChanged(int index, ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int size)) _cnnChannels[index] = size;
        }

        private bool CanCreateBrain =>
            !string.IsNullOrWhiteSpace(EffectiveBuildBrainName)
            && (_includeBarWindow || _includeVolumeProfile || _includeTimeProfile || _includeSessionClock || _includeSpreadVolatility)
            && (_includeHold || _includeEnterLong || _includeEnterShort || _includeClose)
            && (_architectureKind is not (ArchitectureKind.Lstm or ArchitectureKind.Cnn) || _includeBarWindow)
            && (_architectureKind != ArchitectureKind.Cnn || (_cnnKernelSize >= 1 && EffectiveCnnChannels.Count > 0))
            && (!_includeBarWindow || _barWindowSize >= 1)
            && (!_includeVolumeProfile || (_volumeProfileBucketCount >= 1 && _volumeProfileLookbackBars >= 1))
            && (!_includeTimeProfile || (_timeProfileBucketCount >= 1 && _timeProfileLookbackBars >= 1))
            && (!_includeSpreadVolatility || _spreadVolatilityAtrPeriod >= 1)
            && _decisionTemperature > 0;

        private IReadOnlyList<EncoderKind> SelectedEncoderKinds
        {
            get
            {
                var kinds = new List<EncoderKind>();
                if (_includeBarWindow) kinds.Add(EncoderKind.BarWindow);
                if (_includeVolumeProfile) kinds.Add(EncoderKind.VolumeProfile);
                if (_includeTimeProfile) kinds.Add(EncoderKind.TimeProfile);
                if (_includeSessionClock) kinds.Add(EncoderKind.SessionClock);
                if (_includeSpreadVolatility) kinds.Add(EncoderKind.SpreadVolatility);
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
            TimeProfileSplitUpDown: _timeProfileSplitUpDown);

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
            new TraderStateEncoder().FeatureCount
            + SelectedEncoderKinds.Sum(k => BrainCatalog.CreateEncoder(k, CurrentEncoderSizeOptions).FeatureCount);

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
                if (_includeHold) labels.Add("Hold");
                if (_includeEnterLong) labels.Add("Enter Long");
                if (_includeEnterShort) labels.Add("Enter Short");
                if (_includeClose) labels.Add("Close");
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
                    ArchitectureKind.Cnn => $"K{_cnnKernelSize}-" + string.Join("-", EffectiveCnnChannels.Select(n => $"C{n}")),
                    _ => $"H{_lstmHiddenSize}-N{_lstmNumLayers}",
                };

                var dataParts = new List<string>();
                if (_includeBarWindow) dataParts.Add(_barWindowNormalization == NormalizationKind.Raw ? "dBWR" : "dBW");
                if (_includeVolumeProfile)
                {
                    string vpTag = _volumeProfileNormalization == NormalizationKind.Raw ? "dVPR" : "dVP";
                    dataParts.Add(_volumeProfileSplitUpDown ? $"{vpTag}-split" : vpTag);
                }
                if (_includeTimeProfile)
                {
                    string tpTag = _timeProfileNormalization == NormalizationKind.Raw ? "dTPR" : "dTP";
                    dataParts.Add(_timeProfileSplitUpDown ? $"{tpTag}-split" : tpTag);
                }
                if (_includeSessionClock) dataParts.Add("dSC");
                if (_includeSpreadVolatility) dataParts.Add("dSV");
                dataParts.Add("dAS");
                string data = string.Join("-", dataParts);

                string name = string.IsNullOrEmpty(layers) ? $"{arch}_{data}" : $"{arch}_{layers}_{data}";

                var actionParts = new List<string>();
                if (!_includeHold) actionParts.Add("noHOLD");
                if (!_includeEnterLong) actionParts.Add("noLONG");
                if (!_includeEnterShort) actionParts.Add("noSHORT");
                if (!_includeClose) actionParts.Add("noCLOSE");
                if (_includeVolumeOutput) actionParts.Add("aVOL");
                if (_includeStopLossOutput) actionParts.Add("aSL");
                if (_includeTakeProfitOutput) actionParts.Add("aTP");
                if (_decisionDecoding == DecisionDecodingKind.HardMax) actionParts.Add("hardmax");
                else if (_decisionTemperature != 1.0) actionParts.Add($"T{_decisionTemperature:0.##}");
                return actionParts.Count == 0 ? name : $"{name}_{string.Join("-", actionParts)}";
            }
        }

        private string EffectiveBuildBrainName => _buildBrainNameAuto ? AutoBuildBrainName : _buildBrainName;

        private void OnBuildBrainNameInput(ChangeEventArgs e)
        {
            _buildBrainNameAuto = false;
            _buildBrainName = e.Value?.ToString() ?? "";
        }

        private void OnToggleAutoBuildBrainName(ChangeEventArgs e)
        {
            _buildBrainNameAuto = e.Value is bool isAuto && isAuto;
            if (!_buildBrainNameAuto) _buildBrainName = AutoBuildBrainName;
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
                if (_includeVolumeOutput) kinds.Add(ActionOutputKind.Volume);
                if (_includeStopLossOutput) kinds.Add(ActionOutputKind.StopLoss);
                if (_includeTakeProfitOutput) kinds.Add(ActionOutputKind.TakeProfit);
                return kinds;
            }
        }

        private List<SimulationAction> SelectedEnabledDirections
        {
            get
            {
                var directions = new List<SimulationAction>();
                if (_includeHold) directions.Add(SimulationAction.Hold);
                if (_includeEnterLong) directions.Add(SimulationAction.EnterLong);
                if (_includeEnterShort) directions.Add(SimulationAction.EnterShort);
                if (_includeClose) directions.Add(SimulationAction.Close);
                return directions;
            }
        }

        private BrainRecipe CurrentRecipe => new(
            SelectedEncoderKinds.ToList(), _architectureKind, CurrentHiddenSizes.ToList(),
            SelectedActionOutputKinds.Count > 0 ? SelectedActionOutputKinds : null, ActionOutputEncodingKind.DiscretizedLevels,
            SelectedEnabledDirections.Count > 0 ? SelectedEnabledDirections : null, CurrentEncoderSizeOptions,
            _decisionDecoding, _decisionTemperature);

        private void CreateBrain()
        {
            var name = EffectiveBuildBrainName;

            if (Service.TryBuildBrain(
                    name, SelectedEncoderKinds, _architectureKind, CurrentHiddenSizes,
                    SelectedActionOutputKinds, SelectedEnabledDirections, CurrentEncoderSizeOptions,
                    _decisionDecoding, _decisionTemperature))
            {
                _buildBrainStatus = $"built '{name}' -- pick it above to train it";
                _buildBrainName = "";
                _buildBrainNameAuto = true;
                _ = OnBrainCreated.InvokeAsync(name);
            }
            else
            {
                _buildBrainStatus = Service.Error;
            }
        }
    }
}
