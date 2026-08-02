using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Training
{
    // One decoded action set (Direction/Volume/Stop Loss/Take Profit) as a
    // logit/label/percentage/bar grid, with a top badge for the decode rule
    // (softmax/argmax) -- reused once per group in TrainingPreview's Actions
    // column (see TrainingPreview.ActionDecoderGroups). Purely presentational
    // -- Group already carries everything it needs to render.
    public partial class ActionSetDecoder : ComponentBase
    {
        [Parameter, EditorRequired] public ActionDecoderGroup Group { get; set; } = default!;
    }
}
