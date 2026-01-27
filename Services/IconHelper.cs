using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Helper class to extract and cache application icons.
/// </summary>
public class IconHelper
{
    private static readonly ConcurrentDictionary<string, ImageSource?> _iconCache = new();
    private static ImageSource? _defaultIcon;
    private static ImageSource? _webIcon;

    /// <summary>
    /// Clears the icon cache (useful when icon style changes).
    /// </summary>
    public static void ClearCache()
    {
        _iconCache.Clear();
        _defaultIcon = null;
        _webIcon = null;
    }

    /// <summary>
    /// Gets the icon for an application asynchronously.
    /// </summary>
    /// <param name="appPath">Full path to the executable (preferred).</param>
    /// <param name="appName">Name of the application (fallback for running process lookup).</param>
    /// <param name="websiteDomain">Website domain for web apps (e.g., "youtube.com").</param>
    /// <returns>An ImageSource for the icon, or a default icon if not found.</returns>
    public static async Task<ImageSource?> GetIconAsync(string? appPath, string? appName, string? websiteDomain = null)
    {
        // Determine cache key - use domain for web apps, path for desktop apps
        var cacheKey = websiteDomain ?? appPath ?? appName ?? "";
        if (string.IsNullOrEmpty(cacheKey))
            return GetDefaultIcon();

        // Check cache first
        if (_iconCache.TryGetValue(cacheKey, out var cachedIcon))
            return cachedIcon ?? GetDefaultIcon();

        ImageSource? icon = null;

        // Check if this is a web app (has domain OR domain-like name)
        var isWeb = !string.IsNullOrEmpty(websiteDomain) || IsWebApp(appName);
        
        if (isWeb)
        {
            // Use actual domain if available, otherwise try to convert friendly name to domain
            var domainForFavicon = websiteDomain 
                ?? ScreenTimeTracker.SystemUtils.BrowserTabResolver.GetDomainFromFriendlyName(appName)
                ?? appName;
            
            // Try to get real favicon
            icon = await FaviconHelper.GetFaviconAsync(domainForFavicon!);
            
            // If found, cache and return
            if (icon != null)
            {
                _iconCache[cacheKey] = icon;
                return icon;
            }
            
            // If not found, fall through to try getting icon from AppPath (the browser exe)
            // This handles the "Fall back to browser icon" requirement
        }

        // Try to get icon from path if available
        if (!string.IsNullOrEmpty(appPath) && System.IO.File.Exists(appPath))
        {
            icon = ExtractIconFromPath(appPath);
        }

        // Fallback: try to find running process by name
        if (icon == null && !string.IsNullOrEmpty(appName))
        {
            icon = TryGetIconFromRunningProcess(appName);
        }
        
        // Final fallback for web apps that failed everything -> Globe Icon
        if (icon == null && isWeb)
        {
             icon = GetWebIcon();
        }

        // Cache the result (even if null/default, to avoid re-trying)
        _iconCache[cacheKey] = icon;

        return icon ?? GetDefaultIcon();
    }

    /// <summary>
    /// Checks if the app name looks like a web domain.
    /// </summary>
    private static bool IsWebApp(string? appName)
    {
        if (string.IsNullOrEmpty(appName))
            return false;

        // Common TLDs that indicate a website
        string[] webIndicators = { ".com", ".org", ".net", ".io", ".ai", ".dev", ".co", ".me", ".app", ".tv", ".edu", ".gov" };
        var lowerName = appName.ToLowerInvariant();
        
        foreach (var indicator in webIndicators)
        {
            if (lowerName.EndsWith(indicator) || lowerName.Contains(indicator + "/"))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Extracts an icon from an executable path.
    /// </summary>
    private static ImageSource? ExtractIconFromPath(string path)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon != null)
            {
                return ConvertIconToImageSource(icon);
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    /// <summary>
    /// Tries to get an icon from a currently running process.
    /// </summary>
    private static ImageSource? TryGetIconFromRunningProcess(string appName)
    {
        try
        {
            // Remove .exe extension if present for GetProcessesByName
            var processName = appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? appName[..^4]
                : appName;

            var processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0)
            {
                try
                {
                    var path = processes[0].MainModule?.FileName;
                    if (!string.IsNullOrEmpty(path))
                    {
                        return ExtractIconFromPath(path);
                    }
                }
                catch
                {
                    // Access denied or process exited
                }
                finally
                {
                    foreach (var p in processes)
                        p.Dispose();
                }
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }

    /// <summary>
    /// Converts a System.Drawing.Icon to a WPF ImageSource.
    /// </summary>
    private static ImageSource? ConvertIconToImageSource(Icon icon)
    {
        try
        {
            var bitmap = icon.ToBitmap();
            var hBitmap = bitmap.GetHbitmap();

            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
                bitmap.Dispose();
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a default icon for apps without an icon.
    /// </summary>
    private static ImageSource? GetDefaultIcon()
    {
        if (_defaultIcon != null)
            return _defaultIcon;

        try
        {
            // Create a rich default icon (Gradient rounded square with terminal prompt >_)
            var size = 64; // High res
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Background Gradient
                var gradient = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 1)
                };
                gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(75, 85, 99), 0.0)); // slate-600
                gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(55, 65, 81), 1.0)); // slate-700

                context.DrawRoundedRectangle(
                    gradient,
                    null,
                    new System.Windows.Rect(0, 0, size, size),
                    8, 8); // Rounded corners
                
                // Content: Simple terminal prompt shape ">_" to represent "App"
                var pen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Colors.White), 4);
                pen.EndLineCap = PenLineCap.Round;
                pen.StartLineCap = PenLineCap.Round;

                // ">"
                context.DrawLine(pen, new System.Windows.Point(18, 22), new System.Windows.Point(30, 32));
                context.DrawLine(pen, new System.Windows.Point(30, 32), new System.Windows.Point(18, 42));

                // "_"
                context.DrawLine(pen, new System.Windows.Point(36, 42), new System.Windows.Point(46, 42));
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            _defaultIcon = bitmap;
        }
        catch
        {
            // Return null if we can't create a default
        }

        return _defaultIcon;
    }

    /// <summary>
    /// Gets a globe icon for web apps.
    /// </summary>
    private static ImageSource? GetWebIcon()
    {
        if (_webIcon != null)
            return _webIcon;

        try
        {
            var size = 64; // High res
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Background Gradient (Ocean Blue)
                var gradient = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(0, 1)
                };
                gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(59, 130, 246), 0.0)); // blue-500
                gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(37, 99, 235), 1.0)); // blue-600

                context.DrawRoundedRectangle(
                    gradient,
                    null,
                    new System.Windows.Rect(0, 0, size, size),
                    8, 8); // Rounded corners

                // Globe Icon (White lines)
                var borderPen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Colors.White), 3);
                
                // Main Circle
                context.DrawEllipse(null, borderPen, new System.Windows.Point(32, 32), 20, 20);

                // Equator
                context.DrawLine(borderPen, new System.Windows.Point(12, 32), new System.Windows.Point(52, 32));

                // Meridian (Vertical Ellipse)
                context.DrawEllipse(null, borderPen, new System.Windows.Point(32, 32), 8, 20);
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            _webIcon = bitmap;
        }
        catch
        {
            return GetDefaultIcon();
        }

        return _webIcon;
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);
}
