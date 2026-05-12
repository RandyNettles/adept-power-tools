namespace AdeptTools.Launcher.Services;

/// <summary>
/// Singleton that tracks whether mock mode is active at runtime.
/// Services check this to decide whether to use real or mock behavior.
/// </summary>
public class MockModeState
{
    private bool _isMock;

    public bool IsMock
    {
        get => _isMock;
        set
        {
            if (_isMock == value) return;
            _isMock = value;
            Changed?.Invoke(this, value);
        }
    }

    public event EventHandler<bool>? Changed;
}
