using System;
using System.Threading;

namespace Poink.Sim.Components.Layout;

public partial class TerminalLoader : IDisposable
{
    private bool IsLoading = true;
    private string _spinnerText = ">";
    private System.Threading.Timer? _timer;
    private System.Threading.Timer? _spinnerTimer;
    private int _spinnerIndex = 0;
    private string[] _spinnerFrames = { ">", ">>", ">>>", ">>>>", ">>>>>", ">>>>>>", ">>>>>>>" };

    protected override void OnInitialized()
    {
        _spinnerTimer = new System.Threading.Timer(async _ =>
        {
            _spinnerIndex = (_spinnerIndex + 1) % _spinnerFrames.Length;
            _spinnerText = _spinnerFrames[_spinnerIndex];
            await InvokeAsync(StateHasChanged);
        }, null, 0, 100);

        _timer = new System.Threading.Timer(async _ =>
        {
            IsLoading = false;
            await InvokeAsync(StateHasChanged);
            _spinnerTimer?.Dispose();
            _timer?.Dispose();
        }, null, 2000, Timeout.Infinite);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _spinnerTimer?.Dispose();
    }
}
