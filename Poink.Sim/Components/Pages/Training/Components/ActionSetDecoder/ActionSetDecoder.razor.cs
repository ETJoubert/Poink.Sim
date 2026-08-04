using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Training
{

    public partial class ActionSetDecoder : ComponentBase
    {
        [Parameter, EditorRequired] public ActionDecoderGroup Group { get; set; } = default!;
    }
}
