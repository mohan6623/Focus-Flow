using System.Windows;
using Microsoft.Win32;
using ScreenTimeTracker.Storage;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Services;

public enum ThemeType
{
    Light,
    Dark,
    System
}

public class ThemeManager
{
    private const string LightThemeSource = "App/Themes/LightTheme.xaml";
    private const string DarkThemeSource = "App/Themes/DarkTheme.xaml";
    private const string ThemeSettingKey = "theme";
    
    private readonly UsageRepository? _repository;
    
    /// <summary>
    /// The user's selected theme preference (may be System).
    /// </summary>
    public ThemeType SelectedTheme { get; private set; } = ThemeType.Light;
    
    /// <summary>
    /// The actual applied theme (Light or Dark, never System).
    /// </summary>
    public ThemeType CurrentTheme { get; private set; } = ThemeType.Light;

    /// <summary>
    /// Creates a ThemeManager without persistence (for testing).
    /// </summary>
    public ThemeManager() : this(null) { }

    /// <summary>
    /// Creates a ThemeManager with database persistence.
    /// </summary>
    public ThemeManager(UsageRepository? repository)
    {
        _repository = repository;
        LoadSavedTheme();
    }

    private void LoadSavedTheme()
    {
        if (_repository == null) return;

        try
        {
            var savedTheme = _repository.GetSetting(ThemeSettingKey);
            if (!string.IsNullOrEmpty(savedTheme) && Enum.TryParse<ThemeType>(savedTheme, out var parsed))
            {
                SelectedTheme = parsed;
                Logger.Info($"Loaded saved theme preference: {SelectedTheme}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load saved theme: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies the currently selected theme.
    /// </summary>
    public void ApplySelectedTheme()
    {
        ApplyTheme(SelectedTheme);
    }

    /// <summary>
    /// Applies a specific theme and saves the preference.
    /// </summary>
    public void ApplyTheme(ThemeType theme)
    {
        SelectedTheme = theme;
        
        // Resolve System theme to actual Light/Dark
        var actualTheme = theme == ThemeType.System ? GetSystemTheme() : theme;
        
        var dict = new ResourceDictionary();
        string source = actualTheme == ThemeType.Dark ? DarkThemeSource : LightThemeSource;
        
        try 
        {
            // Skip if WPF Application is not yet ready
            if (System.Windows.Application.Current == null)
            {
                CurrentTheme = actualTheme;
                SaveThemePreference();
                return;
            }
            
            dict.Source = new Uri(source, UriKind.RelativeOrAbsolute);
            
            // Clear old theme dictionaries and apply new one
            System.Windows.Application.Current.Resources.MergedDictionaries.Clear();
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(dict);
             
            CurrentTheme = actualTheme;
            SaveThemePreference();
            
            Logger.Info($"Applied theme: {theme} (actual: {actualTheme})");
        }
        catch (Exception ex) 
        {
            Logger.Error($"Failed to apply theme: {ex.Message}");
        }
    }

    private void SaveThemePreference()
    {
        if (_repository == null) return;

        try
        {
            _repository.SetSetting(ThemeSettingKey, SelectedTheme.ToString());
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save theme preference: {ex.Message}");
        }
    }

    /// <summary>
    /// Cycles through themes: Light → Dark → System → Light.
    /// </summary>
    public void CycleTheme()
    {
        var nextTheme = SelectedTheme switch
        {
            ThemeType.Light => ThemeType.Dark,
            ThemeType.Dark => ThemeType.System,
            ThemeType.System => ThemeType.Light,
            _ => ThemeType.Light
        };
        
        ApplyTheme(nextTheme);
    }

    /// <summary>
    /// Gets the system's current theme preference from Windows Registry.
    /// </summary>
    private ThemeType GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            
            // AppsUseLightTheme: 0 = Dark, 1 = Light
            if (value is int intValue)
            {
                return intValue == 0 ? ThemeType.Dark : ThemeType.Light;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get system theme: {ex.Message}");
        }

        return ThemeType.Light; // Default to Light if detection fails
    }

    /// <summary>
    /// Legacy method for backwards compatibility.
    /// </summary>
    public void ToggleTheme()
    {
        CycleTheme();
    }
}
