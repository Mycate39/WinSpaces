using WinSpaces.ViewModels;
using WinSpaces.Modules.Theming.Services;

namespace WinSpaces.Modules.Theming.ViewModels;

public sealed class ThemeSettingsViewModel : ViewModelBase
{
    private readonly ThemeEngine _themeEngine;
    private string _currentTheme = "DarkTheme";

    public ThemeSettingsViewModel(ThemeEngine themeEngine)
    {
        _themeEngine = themeEngine;
    }

    public string CurrentTheme
    {
        get => _currentTheme;
        set
        {
            if (SetProperty(ref _currentTheme, value))
            {
                _themeEngine.ApplyTheme(value);
            }
        }
    }
}
