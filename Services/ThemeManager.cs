using System.Windows;

namespace ScreenTimeTracker.Services;

public enum ThemeType
{
    Light,
    Dark
}

public class ThemeManager
{
    private const string LightThemeSource = "App/Themes/LightTheme.xaml";
    private const string DarkThemeSource = "App/Themes/DarkTheme.xaml";
    
    public ThemeType CurrentTheme { get; private set; } = ThemeType.Light;

    public void ApplyTheme(ThemeType theme)
    {
        var dict = new ResourceDictionary();
        string source = theme == ThemeType.Dark ? DarkThemeSource : LightThemeSource;
        
        try 
        {
            // Skip if WPF Application is not yet ready
            if (System.Windows.Application.Current == null)
            {
                CurrentTheme = theme;
                return;
            }
            
            dict.Source = new Uri(source, UriKind.RelativeOrAbsolute);
            
            // Apply to application resources if possible, otherwise we might need to apply to windows
            // Since we don't have a standard App.xaml execution in this hybrid app, we'll apply to specific dictionary
            
            // Clear old theme dictionaries (assuming they are added to MergedDictionaries)
            // Ideally we'd tag them, but for now we'll just replace implementation
             System.Windows.Application.Current.Resources.MergedDictionaries.Clear();
             System.Windows.Application.Current.Resources.MergedDictionaries.Add(dict);
             
             CurrentTheme = theme;
        }
        catch (Exception ex) 
        {
            // Log error
            System.Diagnostics.Debug.WriteLine($"Failed to apply theme: {ex.Message}");
        }
    }

    public void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == ThemeType.Light ? ThemeType.Dark : ThemeType.Light);
    }
}
