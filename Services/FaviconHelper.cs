using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Helper class to fetch and cache website favicons.
/// </summary>
public class FaviconHelper
{
    private static readonly HttpClient _httpClient = new();
    private static readonly ConcurrentDictionary<string, ImageSource?> _cache = new();
    private static string? _cacheDirectory;

    /// <summary>
    /// Gets the cache directory path.
    /// </summary>
    private static string GetCacheDirectory()
    {
        if (_cacheDirectory == null)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cacheDirectory = Path.Combine(appData, "ScreenTimeTracker", "favicons");
            Directory.CreateDirectory(_cacheDirectory);
        }
        return _cacheDirectory;
    }

    /// <summary>
    /// Gets a favicon for a domain.
    /// Returns null if not found or download fails.
    /// </summary>
    public static async Task<ImageSource?> GetFaviconAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        // Check memory cache
        if (_cache.TryGetValue(domain, out var cached))
            return cached;

        // Check disk cache
        var fileName = $"{domain}.png"; // Simple filename for now (could hash for safety)
        var filePath = Path.Combine(GetCacheDirectory(), fileName);

        if (File.Exists(filePath))
        {
            try
            {
                var bitmap = LoadBitmapFromDisk(filePath);
                _cache[domain] = bitmap;
                return bitmap;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load cached favicon for {domain}: {ex.Message}");
            }
        }

        // Download from network
        try
        {
            // Use Google's S2 service for high-quality favicons (64px)
            var url = $"https://www.google.com/s2/favicons?domain={domain}&sz=64";
            var data = await _httpClient.GetByteArrayAsync(url);

            if (data != null && data.Length > 0)
            {
                // Save to disk
                await File.WriteAllBytesAsync(filePath, data);

                // Load to memory
                var bitmap = LoadBitmapFromBytes(data);
                _cache[domain] = bitmap;
                return bitmap;
            }
        }
        catch (Exception ex)
        {
            // Network error or invalid domain - ignore
            Logger.Debug($"Failed to download favicon for {domain}: {ex.Message}");
        }

        return null;
    }

    private static BitmapImage LoadBitmapFromDisk(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // Load fully into memory
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze(); // Make thread-safe
        return bitmap;
    }

    private static BitmapImage LoadBitmapFromBytes(byte[] data)
    {
        using var stream = new MemoryStream(data);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
