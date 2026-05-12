using CommunityToolkit.Mvvm.ComponentModel;

namespace AdeptTools.Launcher.ViewModels;

public partial class NavItem : ObservableObject
{
    public string Label { get; }
    public string IconGlyph { get; }
    public Type ViewModelType { get; }

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isEnabled;

    public NavItem(string label, string iconGlyph, Type viewModelType, bool isEnabled = true)
    {
        Label = label;
        IconGlyph = iconGlyph;
        ViewModelType = viewModelType;
        _isEnabled = isEnabled;
    }
}
