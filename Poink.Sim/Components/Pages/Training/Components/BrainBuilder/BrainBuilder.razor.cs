using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Training
{
    // Split out of TrainPage so the Brain Type card's "+ BUILD BRAIN" toggle
    // stays there while everything about assembling a brain from encoder/
    // architecture parts lives here instead. Visible is purely presentational
    // (TrainPage still owns _showBuildBrain and the toggle button) rather than
    // an @if around this whole component, so a build left mid-configuration
    // survives the panel being collapsed and reopened, same as before the split.
    public partial class BrainBuilder : ComponentBase
    {
        [Inject] private RlTrainingService Service { get; set; } = default!;

        [Parameter] public bool Visible { get; set; }

        // Fired once TryBuildBrain succeeds, with the new brain's name --
        // TrainPage responds by pointing Brain Type at it and collapsing this
        // panel (see TrainPage.HandleBrainCreated).
        [Parameter] public EventCallback<string> OnBrainCreated { get; set; }

        private bool _wasVisible;

        // Clears any previous build's status message the moment this panel is
        // reopened -- mirrors TrainPage's old ToggleBuildBrain, which cleared
        // it on every toggle; checking the visibility transition instead of
        // just Visible avoids re-clearing on every unrelated re-render.
        protected override void OnParametersSet()
        {
            if (Visible && !_wasVisible) _buildBrainStatus = null;
            _wasVisible = Visible;
        }

        private string _buildBrainName = "";

        // While on, the Name field tracks AutoBuildBrainName below instead of
        // _buildBrainName -- flips off the moment the user edits the field
        // directly (OnBuildBrainNameInput), and back on once a brain is
        // created, so the next build starts auto-named again.
        private bool _buildBrainNameAuto = true;
        private bool _includeBarWindow = true;
        private bool _includeVolumeProfile;
        private bool _includeTimeProfile;

        // Normalized feeds fraction-of-total/percent-offset values (each
        // set's original convention); Raw feeds exact tick counts/seconds and
        // exact price levels instead -- see NormalizationKind's doc comment.
        // A per-set build option, orthogonal to which EncoderKind a set maps
        // to (see SelectedEncoderKinds/CurrentEncoderSizeOptions below).
        private NormalizationKind _barWindowNormalization = NormalizationKind.Normalized;
        private NormalizationKind _volumeProfileNormalization = NormalizationKind.Normalized;
        private NormalizationKind _timeProfileNormalization = NormalizationKind.Normalized;

        // Per-set structural width -- independent of each other, and now
        // build-time-only for Volume/Time Profile's lookback too (see
        // EncoderSizeOptions's doc comment): every one of these is baked into
        // the encoder once at construction, same treatment bucket count
        // always had. Defaults match EncoderSizeOptions' own defaults.
        private int _barWindowSize = 20;
        private int _volumeProfileBucketCount = 20;
        private int _timeProfileBucketCount = 20;
        private int _volumeProfileLookbackBars = 50;
        private int _timeProfileLookbackBars = 50;

        private ArchitectureKind _architectureKind = ArchitectureKind.Mlp;

        // One entry per MLP hidden layer, in network order -- edited via the
        // Structure card's per-layer number inputs (+ ADD LAYER/remove ×)
        // instead of a single comma-delimited text field. Starts as a single
        // layer that RemoveHiddenLayer refuses to drop below -- unlike the old
        // comma-delimited text field, a linear (zero-layer) model is no longer
        // reachable through the UI.
        private List<int> _mlpHiddenSizes = [64];
        private int _lstmHiddenSize = 64;
        private int _lstmNumLayers = 2;

        // Cnn's own knobs -- kernelSize applies to every conv layer (odd
        // sizes keep exact "same" padding, see CnnPolicyNetwork), _cnnChannels
        // is each conv layer's output channel count, in network order --
        // same add/remove-layer editor shape as _mlpHiddenSizes, just widening
        // (doubling) by default instead of narrowing since deeper conv stacks
        // conventionally grow channel depth as they go.
        private int _cnnKernelSize = 3;
        private List<int> _cnnChannels = [16, 32];

        // Which extra (non-direction) decisions the built brain's network
        // also makes -- see ActionOutputKind. All optional (unlike the
        // encoder checkboxes, at least one of which is required).
        private bool _includeVolumeOutput;
        private bool _includeStopLossOutput;
        private bool _includeTakeProfitOutput;

        // Which of Hold/EnterLong/EnterShort/Close the built brain's
        // direction pick is narrowed to -- default all 4 (today's original
        // behavior), but every one is individually optional, same as the
        // action-output checkboxes above (see SimulationAction's doc
        // comment); CanCreateBrain requires at least one stay checked.
        private bool _includeHold = true;
        private bool _includeEnterLong = true;
        private bool _includeEnterShort = true;
        private bool _includeClose = true;
        private string? _buildBrainStatus;

        private void OnArchitectureKindChanged(ChangeEventArgs e) =>
            _architectureKind = Enum.Parse<ArchitectureKind>(e.Value?.ToString() ?? nameof(ArchitectureKind.Mlp));

        private void OnBarWindowNormalizationChanged(ChangeEventArgs e) =>
            _barWindowNormalization = Enum.Parse<NormalizationKind>(e.Value?.ToString() ?? nameof(NormalizationKind.Normalized));

        private void OnVolumeProfileNormalizationChanged(ChangeEventArgs e) =>
            _volumeProfileNormalization = Enum.Parse<NormalizationKind>(e.Value?.ToString() ?? nameof(NormalizationKind.Normalized));

        private void OnTimeProfileNormalizationChanged(ChangeEventArgs e) =>
            _timeProfileNormalization = Enum.Parse<NormalizationKind>(e.Value?.ToString() ?? nameof(NormalizationKind.Normalized));

        // Mirrors the old ParseHiddenSizes(text)'s n > 0 filter -- a layer
        // cleared to 0 (or left blank) drops out rather than building a
        // zero-width layer.
        private IReadOnlyList<int> EffectiveMlpHiddenSizes => _mlpHiddenSizes.Where(n => n > 0).ToList();

        // New layer defaults to half the layer it's stacked after -- an MLP
        // narrows toward its output, so this continues that taper instead of
        // reintroducing a flat 16 every time.
        private void AddHiddenLayer() => _mlpHiddenSizes.Add(Math.Max(1, _mlpHiddenSizes[^1] / 2));

        // New layer defaults to double layer 1 -- the inverse taper of
        // AddHiddenLayer above, widening back out as layers are stacked
        // before the existing first one.
        private void AddHiddenLayerOnTop() => _mlpHiddenSizes.Insert(0, _mlpHiddenSizes[0] * 2);

        // Layer 1 can't be dropped below one entry -- an empty list reads as
        // a linear model, which the UI no longer offers a way back into.
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

        // Mirrors EffectiveMlpHiddenSizes -- a channel count cleared to 0 (or
        // left blank) drops that conv layer out rather than building a
        // zero-channel one.
        private IReadOnlyList<int> EffectiveCnnChannels => _cnnChannels.Where(n => n > 0).ToList();

        // New layer defaults to double the last layer's channel count --
        // unlike AddHiddenLayer's narrowing taper, a deeper conv stack
        // conventionally grows channel depth as it goes.
        private void AddCnnChannelLayer() => _cnnChannels.Add(_cnnChannels[^1] * 2);

        // Layer 1 can't be dropped below one entry, same floor as
        // RemoveHiddenLayer -- a CNN needs at least one conv layer.
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

        // LSTM/Cnn both need a sequence-bearing part (Bar Window) to narrow()
        // a sequence out of -- see LstmArchitecture's/CnnArchitecture's doc
        // comments; mirrored here so the Create button disables instead of
        // only failing after a click.
        private bool CanCreateBrain =>
            !string.IsNullOrWhiteSpace(EffectiveBuildBrainName)
            && (_includeBarWindow || _includeVolumeProfile || _includeTimeProfile)
            && (_includeHold || _includeEnterLong || _includeEnterShort || _includeClose)
            && (_architectureKind is not (ArchitectureKind.Lstm or ArchitectureKind.Cnn) || _includeBarWindow)
            && (_architectureKind != ArchitectureKind.Cnn || (_cnnKernelSize >= 1 && EffectiveCnnChannels.Count > 0))
            && (!_includeBarWindow || _barWindowSize >= 1)
            && (!_includeVolumeProfile || (_volumeProfileBucketCount >= 1 && _volumeProfileLookbackBars >= 1))
            && (!_includeTimeProfile || (_timeProfileBucketCount >= 1 && _timeProfileLookbackBars >= 1));

        // Canonical part order (Bar Window before Volume Profile before Time
        // Profile) regardless of checkbox click order -- CompositeEncoder
        // requires any sequence-bearing part to be listed first (see its doc
        // comment). Shared by CreateBrain and TotalInputCount so the two never
        // drift apart. EncoderKind no longer encodes Normalization (see
        // CurrentEncoderSizeOptions below) -- picking Raw never changes which
        // kind gets added here, only how CreateEncoder builds it.
        private IReadOnlyList<EncoderKind> SelectedEncoderKinds
        {
            get
            {
                var kinds = new List<EncoderKind>();
                if (_includeBarWindow) kinds.Add(EncoderKind.BarWindow);
                if (_includeVolumeProfile) kinds.Add(EncoderKind.VolumeProfile);
                if (_includeTimeProfile) kinds.Add(EncoderKind.TimeProfile);
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
            TimeProfileNormalization: _timeProfileNormalization);

        private string VolumeProfileBucketsTooltip => _volumeProfileNormalization == NormalizationKind.Raw
            ? "How many histogram buckets Volume Profile rebins its merged bars into -- each bucket feeds price and volume under Raw, so its input count is this times 2 plus 3 summary values"
            : "How many histogram buckets Volume Profile rebins its merged bars into -- its input count is this plus 3 summary values";

        private string TimeProfileBucketsTooltip => _timeProfileNormalization == NormalizationKind.Raw
            ? "How many histogram buckets Time Profile rebins its merged bars into -- each bucket feeds price and seconds under Raw, so its input count is this times 2 plus 3 summary values"
            : "How many histogram buckets Time Profile rebins its merged bars into -- its input count is this plus 3 summary values";

        // Live input-count readout for the Input card -- reuses
        // BrainCatalog.CreateEncoder (the same factory CreateBrain itself
        // calls into via Service.TryBuildBrain) as the single source of truth
        // for each encoder's FeatureCount, instead of duplicating that
        // arithmetic here. Trader State is always included (see BrainCatalog's
        // doc comment on why it's not a pickable EncoderKind).
        private int TotalInputCount =>
            new TraderStateEncoder().FeatureCount
            + SelectedEncoderKinds.Sum(k => BrainCatalog.CreateEncoder(k, CurrentEncoderSizeOptions).FeatureCount);

        // First hidden/conv layer's width -- null for an MLP or Cnn trimmed
        // down to zero layers (a linear model / no conv layers, so there's no
        // first layer to flag).
        private int? FirstHiddenLayerSize => _architectureKind switch
        {
            ArchitectureKind.Mlp => EffectiveMlpHiddenSizes.Count > 0 ? EffectiveMlpHiddenSizes[0] : null,
            ArchitectureKind.Cnn => EffectiveCnnChannels.Count > 0 ? EffectiveCnnChannels[0] : null,
            _ => _lstmHiddenSize,
        };

        // Flags a first layer narrower than the inputs feeding it -- not
        // necessarily wrong, but a common accidental bottleneck worth
        // surfacing in the Structure card header.
        private bool FirstLayerTooSmall => FirstHiddenLayerSize is int size && size < TotalInputCount;

        // Total selectable action decisions -- the 4 directions plus the 3
        // optional output heads below -- shared by the Actions card's badge
        // and its Select All checkbox.
        private const int TotalActionOptionCount = 7;

        private int SelectedActionCount =>
            DirectionCount
            + (_includeVolumeOutput ? 1 : 0)
            + (_includeStopLossOutput ? 1 : 0)
            + (_includeTakeProfitOutput ? 1 : 0);

        // Bound directly to the Actions card's Select All checkbox -- reading
        // it reports whether every option is already on (so the checkbox
        // shows checked once the user gets there by hand too), setting it
        // flips all 7 at once.
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

        // Enabled direction labels, in ComposedPolicyBrain.AllDirections'
        // canonical order -- mirrors CreateBrain's own enabledDirections
        // build, feeds both DirectionCount and the Action Decoding card's
        // per-row tooltip below.
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

        // Live output widths for the Action Decoding card -- reuses
        // BrainCatalog.CreateActionOutputHead (the same factory CreateBrain
        // itself calls into) as the single source of truth for each head's
        // OutputWidth, mirroring TotalInputCount's use of CreateEncoder above.
        private int VolumeOutputWidth =>
            BrainCatalog.CreateActionOutputHead(ActionOutputKind.Volume, ActionOutputEncodingKind.DiscretizedLevels).OutputWidth;

        private int StopLossOutputWidth =>
            BrainCatalog.CreateActionOutputHead(ActionOutputKind.StopLoss, ActionOutputEncodingKind.DiscretizedLevels).OutputWidth;

        private int TakeProfitOutputWidth =>
            BrainCatalog.CreateActionOutputHead(ActionOutputKind.TakeProfit, ActionOutputEncodingKind.DiscretizedLevels).OutputWidth;

        // Total network output width: the direction head's softmax (one
        // logit per enabled direction) plus every enabled extra head's own
        // independent softmax width -- mirrors ComposedPolicyBrain.Create's
        // own enabledDirections.Count + extraOutputWidth sizing.
        private int TotalOutputWidth =>
            DirectionCount
            + (_includeVolumeOutput ? VolumeOutputWidth : 0)
            + (_includeStopLossOutput ? StopLossOutputWidth : 0)
            + (_includeTakeProfitOutput ? TakeProfitOutputWidth : 0);

        // {Architecture}_{Layers}_{Data}, e.g. MLP_L32-L16_dBW-dVP-dTP-dAS -- mirrors
        // CreateBrain's own encoder/architecture choices below, so the generated
        // name always describes exactly what CREATE BRAIN is about to build.
        // Trader State has no checkbox (always included) but still gets a
        // "dAS" tag here for the same reason it gets a disabled-checked
        // checkbox above: visible parity with the other data sources.
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
                if (_includeVolumeProfile) dataParts.Add(_volumeProfileNormalization == NormalizationKind.Raw ? "dVPR" : "dVP");
                if (_includeTimeProfile) dataParts.Add(_timeProfileNormalization == NormalizationKind.Raw ? "dTPR" : "dTP");
                dataParts.Add("dAS");
                string data = string.Join("-", dataParts);

                string name = string.IsNullOrEmpty(layers) ? $"{arch}_{data}" : $"{arch}_{layers}_{data}";

                // Excluded (not included) directions are called out here --
                // shorter than listing all the ones still enabled, and the
                // common case (all 4) needs no tag at all.
                var actionParts = new List<string>();
                if (!_includeHold) actionParts.Add("noHOLD");
                if (!_includeEnterLong) actionParts.Add("noLONG");
                if (!_includeEnterShort) actionParts.Add("noSHORT");
                if (!_includeClose) actionParts.Add("noCLOSE");
                if (_includeVolumeOutput) actionParts.Add("aVOL");
                if (_includeStopLossOutput) actionParts.Add("aSL");
                if (_includeTakeProfitOutput) actionParts.Add("aTP");
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

        private void CreateBrain()
        {
            var name = EffectiveBuildBrainName;
            var encoderKinds = SelectedEncoderKinds;

            IReadOnlyList<int> hiddenSizes = _architectureKind switch
            {
                ArchitectureKind.Mlp => EffectiveMlpHiddenSizes,
                ArchitectureKind.Cnn => [_cnnKernelSize, .. EffectiveCnnChannels],
                _ => [_lstmHiddenSize, _lstmNumLayers],
            };

            var actionOutputKinds = new List<ActionOutputKind>();
            if (_includeVolumeOutput) actionOutputKinds.Add(ActionOutputKind.Volume);
            if (_includeStopLossOutput) actionOutputKinds.Add(ActionOutputKind.StopLoss);
            if (_includeTakeProfitOutput) actionOutputKinds.Add(ActionOutputKind.TakeProfit);

            // Canonical order (matches ComposedPolicyBrain.AllDirections)
            // regardless of checkbox click order.
            var enabledDirections = new List<SimulationAction>();
            if (_includeHold) enabledDirections.Add(SimulationAction.Hold);
            if (_includeEnterLong) enabledDirections.Add(SimulationAction.EnterLong);
            if (_includeEnterShort) enabledDirections.Add(SimulationAction.EnterShort);
            if (_includeClose) enabledDirections.Add(SimulationAction.Close);

            if (Service.TryBuildBrain(name, encoderKinds, _architectureKind, hiddenSizes, actionOutputKinds, enabledDirections, CurrentEncoderSizeOptions))
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
