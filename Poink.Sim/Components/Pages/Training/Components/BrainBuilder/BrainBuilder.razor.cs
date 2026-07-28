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
        private bool _includeVolumeProfileRaw;
        private bool _includeTimeProfileRaw;

        // Per-encoder input width -- independent of each other and of
        // TrainPage's own Profile lookback (see EncoderSizeOptions's doc
        // comment for why these are build-time-only, unlike the
        // runtime-adjustable lookback). Defaults match EncoderSizeOptions'
        // own defaults, today's original hardcoded bucket/window counts.
        private int _barWindowSize = 20;
        private int _volumeProfileBucketCount = 20;
        private int _timeProfileBucketCount = 20;
        private int _volumeProfileRawBucketCount = 20;
        private int _timeProfileRawBucketCount = 20;

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

        // LSTM needs a sequence-bearing part (Bar Window) to narrow() a
        // sequence out of -- see LstmArchitecture's doc comment; mirrored here
        // so the Create button disables instead of only failing after a click.
        private bool CanCreateBrain =>
            !string.IsNullOrWhiteSpace(EffectiveBuildBrainName)
            && (_includeBarWindow || _includeVolumeProfile || _includeTimeProfile || _includeVolumeProfileRaw || _includeTimeProfileRaw)
            && (_includeHold || _includeEnterLong || _includeEnterShort || _includeClose)
            && (_architectureKind != ArchitectureKind.Lstm || _includeBarWindow)
            && (!_includeBarWindow || _barWindowSize >= 1)
            && (!_includeVolumeProfile || _volumeProfileBucketCount >= 1)
            && (!_includeTimeProfile || _timeProfileBucketCount >= 1)
            && (!_includeVolumeProfileRaw || _volumeProfileRawBucketCount >= 1)
            && (!_includeTimeProfileRaw || _timeProfileRawBucketCount >= 1);

        // Canonical part order (Bar Window before Volume Profile before Time
        // Profile) regardless of checkbox click order -- CompositeEncoder
        // requires any sequence-bearing part to be listed first (see its doc
        // comment). Shared by CreateBrain and TotalInputCount so the two never
        // drift apart.
        private IReadOnlyList<EncoderKind> SelectedEncoderKinds
        {
            get
            {
                var kinds = new List<EncoderKind>();
                if (_includeBarWindow) kinds.Add(EncoderKind.BarWindow);
                if (_includeVolumeProfile) kinds.Add(EncoderKind.VolumeProfile);
                if (_includeTimeProfile) kinds.Add(EncoderKind.TimeProfile);
                if (_includeVolumeProfileRaw) kinds.Add(EncoderKind.VolumeProfileRaw);
                if (_includeTimeProfileRaw) kinds.Add(EncoderKind.TimeProfileRaw);
                return kinds;
            }
        }

        private EncoderSizeOptions CurrentEncoderSizeOptions => new(
            _barWindowSize, _volumeProfileBucketCount, _timeProfileBucketCount, _volumeProfileRawBucketCount, _timeProfileRawBucketCount);

        // Live input-count readout for the Input Structure card -- reuses
        // BrainCatalog.CreateEncoder (the same factory CreateBrain itself
        // calls into via Service.TryBuildBrain) as the single source of truth
        // for each encoder's FeatureCount, instead of duplicating that
        // arithmetic here. Trader State is always included (see BrainCatalog's
        // doc comment on why it's not a pickable EncoderKind).
        private int TotalInputCount =>
            new TraderStateEncoder().FeatureCount
            + SelectedEncoderKinds.Sum(k => BrainCatalog.CreateEncoder(k, CurrentEncoderSizeOptions).FeatureCount);

        // First hidden layer's width -- null for an MLP trimmed down to zero
        // layers (a linear model, so there's no first layer to flag).
        private int? FirstHiddenLayerSize => _architectureKind == ArchitectureKind.Mlp
            ? (EffectiveMlpHiddenSizes.Count > 0 ? EffectiveMlpHiddenSizes[0] : null)
            : _lstmHiddenSize;

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

                string layers = _architectureKind == ArchitectureKind.Mlp
                    ? string.Join("-", EffectiveMlpHiddenSizes.Select(n => $"L{n}"))
                    : $"H{_lstmHiddenSize}-N{_lstmNumLayers}";

                var dataParts = new List<string>();
                if (_includeBarWindow) dataParts.Add("dBW");
                if (_includeVolumeProfile) dataParts.Add("dVP");
                if (_includeTimeProfile) dataParts.Add("dTP");
                if (_includeVolumeProfileRaw) dataParts.Add("dVPR");
                if (_includeTimeProfileRaw) dataParts.Add("dTPR");
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

            IReadOnlyList<int> hiddenSizes = _architectureKind == ArchitectureKind.Mlp
                ? EffectiveMlpHiddenSizes
                : [_lstmHiddenSize, _lstmNumLayers];

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
