using System.Runtime.InteropServices;
using System.Windows.Automation;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.SystemUtils;

/// <summary>
/// Extracts the current URL/Tab from browser windows using UI Automation.
/// Supports Chrome, Edge, Firefox, Brave, Opera, and Vivaldi.
/// </summary>
public class BrowserTabResolver
{
    // Known browser process names (without .exe extension, as Process.ProcessName returns)
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome",
        "msedge",
        "firefox",
        "brave",
        "opera",
        "vivaldi",
        "iexplore",
        "arc"
    };

    // Map of well-known domains to friendly names
    private static readonly Dictionary<string, string> WellKnownSites = new(StringComparer.OrdinalIgnoreCase)
    {
        { "youtube.com", "YouTube" },
        { "google.com", "Google" },
        { "facebook.com", "Facebook" },
        { "twitter.com", "Twitter" },
        { "x.com", "Twitter" },
        { "instagram.com", "Instagram" },
        { "linkedin.com", "LinkedIn" },
        { "github.com", "GitHub" },
        { "reddit.com", "Reddit" },
        { "netflix.com", "Netflix" },
        { "amazon.com", "Amazon" },
        { "twitch.tv", "Twitch" },
        { "spotify.com", "Spotify" },
        { "discord.com", "Discord" },
        { "slack.com", "Slack" },
        { "stackoverflow.com", "StackOverflow" },
        { "chatgpt.com", "ChatGPT" },
        { "openai.com", "OpenAI" },
        { "claude.ai", "Claude" },
        { "gemini.google.com", "Gemini" },
        { "mail.google.com", "Gmail" },
        { "drive.google.com", "Google Drive" },
        { "docs.google.com", "Google Docs" },
        { "sheets.google.com", "Google Sheets" },
        { "outlook.live.com", "Outlook" },
        { "outlook.office.com", "Outlook" },
        { "notion.so", "Notion" },
        { "figma.com", "Figma" },
        { "trello.com", "Trello" },
        { "asana.com", "Asana" },
        { "zoom.us", "Zoom" },
        { "meet.google.com", "Google Meet" },
        { "teams.microsoft.com", "Microsoft Teams" },
        { "whatsapp.com", "WhatsApp" },
        { "web.whatsapp.com", "WhatsApp" },
        { "messenger.com", "Messenger" }
    };

    #region Native Imports

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    #endregion

    /// <summary>
    /// Checks if the given process is a known browser.
    /// </summary>
    public bool IsBrowser(string processName)
    {
        var normalized = processName.ToLowerInvariant().Replace(".exe", "");
        return BrowserProcesses.Contains(normalized);
    }

    /// <summary>
    /// Gets a friendly name for a well-known domain, or returns the domain itself.
    /// </summary>
    public static string GetFriendlyName(string? domain)
    {
        if (string.IsNullOrEmpty(domain))
            return string.Empty;

        // Check if it's a well-known site
        if (WellKnownSites.TryGetValue(domain, out var friendlyName))
            return friendlyName;

        // Check subdomains (e.g., www.youtube.com -> YouTube)
        foreach (var kvp in WellKnownSites)
        {
            if (domain.EndsWith("." + kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                domain.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        // Return the domain with first letter capitalized
        return char.ToUpperInvariant(domain[0]) + domain[1..];
    }

    /// <summary>
    /// Attempts to extract the current URL from a browser window using UI Automation.
    /// Returns the domain (e.g., "youtube.com") or null if extraction fails.
    /// Includes a retry mechanism to handle cases where the URL isn't immediately available.
    /// </summary>
    public string? GetBrowserUrl(IntPtr windowHandle, string processName)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 50;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // First try UI Automation (most reliable)
                var url = GetUrlViaUIAutomation(windowHandle, processName);
                if (!string.IsNullOrEmpty(url))
                {
                    Logger.Debug($"Got URL via UI Automation: {url}");
                    return url;
                }

                // Fallback to window title parsing
                url = GetUrlFromWindowTitle(windowHandle, processName);
                if (!string.IsNullOrEmpty(url))
                {
                    Logger.Debug($"Got URL from window title: {url}");
                    return url;
                }

                // If this isn't the last attempt, wait a bit and retry
                if (attempt < maxRetries - 1)
                {
                    System.Threading.Thread.Sleep(retryDelayMs);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to get browser URL (attempt {attempt + 1}): {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Uses UI Automation to find the address bar and extract the URL.
    /// </summary>
    private string? GetUrlViaUIAutomation(IntPtr windowHandle, string processName)
    {
        try
        {
            var automationElement = AutomationElement.FromHandle(windowHandle);
            if (automationElement == null)
                return null;

            var normalizedName = processName.ToLowerInvariant().Replace(".exe", "");

            // For Chromium-based browsers (Chrome, Edge, Brave, Vivaldi, Opera)
            if (IsChromiumBrowser(processName))
            {
                return GetChromiumUrl(automationElement);
            }

            // For Firefox
            if (normalizedName == "firefox")
            {
                return GetFirefoxUrl(automationElement);
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Debug($"UI Automation error: {ex.Message}");
            return null;
        }
    }

    private static bool IsChromiumBrowser(string processName)
    {
        var chromiumBrowsers = new[] { "chrome", "msedge", "brave", "vivaldi", "opera", "arc" };
        return chromiumBrowsers.Contains(processName.ToLowerInvariant().Replace(".exe", ""));
    }

    private string? GetChromiumUrl(AutomationElement browserWindow)
    {
        try
        {
            // Chromium browsers have an edit control with the URL
            // Search for edit controls (address bar)
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            var editElements = browserWindow.FindAll(TreeScope.Descendants, condition);

            foreach (AutomationElement edit in editElements)
            {
                try
                {
                    // Check if this is the address bar by looking at the name
                    var name = edit.Current.Name?.ToLowerInvariant() ?? "";
                    if (name.Contains("address") || name.Contains("url") || name.Contains("search") ||
                        name.Contains("omnibox") || name.Contains("location"))
                    {
                        if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern))
                        {
                            var valuePattern = (ValuePattern)pattern;
                            var url = valuePattern.Current.Value;
                            if (!string.IsNullOrEmpty(url) && (url.Contains("://") || url.Contains(".")))
                            {
                                return url;
                            }
                        }
                    }
                }
                catch
                {
                    // Continue to next element
                }
            }

            // Alternative: Try to find by looking for edit controls with URL-like values
            foreach (AutomationElement edit in editElements)
            {
                try
                {
                    if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern))
                    {
                        var valuePattern = (ValuePattern)pattern;
                        var value = valuePattern.Current.Value;
                        
                        // Check if this looks like a URL
                        if (!string.IsNullOrEmpty(value) && 
                            (value.StartsWith("http://") || value.StartsWith("https://") ||
                             (value.Contains(".") && !value.Contains(" ") && value.Length > 3)))
                        {
                            return value;
                        }
                    }
                }
                catch
                {
                    // Continue to next element
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Error getting Chromium URL: {ex.Message}");
        }

        return null;
    }

    private string? GetFirefoxUrl(AutomationElement browserWindow)
    {
        try
        {
            // Firefox uses a different structure - look for combo box or edit
            var comboCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox);
            var comboElements = browserWindow.FindAll(TreeScope.Descendants, comboCondition);

            foreach (AutomationElement combo in comboElements)
            {
                try
                {
                    var name = combo.Current.Name?.ToLowerInvariant() ?? "";
                    if (name.Contains("address") || name.Contains("url") || name.Contains("search"))
                    {
                        if (combo.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern))
                        {
                            var valuePattern = (ValuePattern)pattern;
                            var url = valuePattern.Current.Value;
                            if (!string.IsNullOrEmpty(url))
                            {
                                return url;
                            }
                        }
                    }
                }
                catch
                {
                    // Continue
                }
            }

            // Fallback to edit controls
            var editCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            var editElements = browserWindow.FindAll(TreeScope.Descendants, editCondition);

            foreach (AutomationElement edit in editElements)
            {
                try
                {
                    if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern))
                    {
                        var valuePattern = (ValuePattern)pattern;
                        var value = valuePattern.Current.Value;
                        
                        if (!string.IsNullOrEmpty(value) && 
                            (value.StartsWith("http://") || value.StartsWith("https://") ||
                             (value.Contains(".") && !value.Contains(" "))))
                        {
                            return value;
                        }
                    }
                }
                catch
                {
                    // Continue
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Error getting Firefox URL: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Fallback method: Try to extract domain from window title.
    /// Many browsers show "Page Title - Site Name" or "Page Title - URL - Browser".
    /// </summary>
    private string? GetUrlFromWindowTitle(IntPtr windowHandle, string processName)
    {
        try
        {
            var title = GetWindowTitle(windowHandle);
            if (string.IsNullOrEmpty(title))
                return null;

            // Browser-specific title parsing
            // Chrome/Edge/Brave format: "Page Title - Google Chrome" or "Page Title - YouTube - Google Chrome"
            // Firefox format: "Page Title — Mozilla Firefox"
            
            var browserName = GetBrowserDisplayName(processName);
            
            // Remove browser name suffix
            if (!string.IsNullOrEmpty(browserName))
            {
                var suffixIndex = title.LastIndexOf(" - " + browserName, StringComparison.OrdinalIgnoreCase);
                if (suffixIndex == -1)
                    suffixIndex = title.LastIndexOf(" — " + browserName, StringComparison.OrdinalIgnoreCase);
                
                if (suffixIndex > 0)
                {
                    title = title[..suffixIndex];
                }
            }

            // Now try to find a domain in the remaining title
            var parts = title.Split(new[] { " - ", " — ", " | ", " : " }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts.Reverse())
            {
                var trimmed = part.Trim();
                
                // Check if this is a well-known site name
                foreach (var kvp in WellKnownSites)
                {
                    if (trimmed.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp.Key;
                    }
                }
                
                // Check if this looks like a domain
                if (LooksLikeDomain(trimmed))
                {
                    return trimmed.ToLowerInvariant();
                }
            }

            // Check the first part for site names like "YouTube" in "YouTube - My Video"
            if (parts.Length > 0)
            {
                var firstPart = parts[0].Trim();
                foreach (var kvp in WellKnownSites)
                {
                    if (firstPart.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp.Key;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Debug($"Error extracting URL from window title: {ex.Message}");
            return null;
        }
    }

    private static string? GetBrowserDisplayName(string processName)
    {
        return processName.ToLowerInvariant().Replace(".exe", "") switch
        {
            "chrome" => "Google Chrome",
            "msedge" => "Microsoft Edge",
            "firefox" => "Mozilla Firefox",
            "brave" => "Brave",
            "vivaldi" => "Vivaldi",
            "opera" => "Opera",
            "arc" => "Arc",
            _ => null
        };
    }

    private static bool LooksLikeDomain(string text)
    {
        // Simple heuristic: contains a dot, no spaces, looks like a domain
        if (string.IsNullOrEmpty(text) || text.Contains(' ') || text.Length < 3)
            return false;

        var tlds = new[] { ".com", ".org", ".net", ".io", ".co", ".tv", ".edu", ".gov", ".in", ".uk", ".ai", ".so", ".us", ".dev", ".app", ".me" };
        return tlds.Any(tld => text.EndsWith(tld, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts domain from a full URL.
    /// </summary>
    public static string? ExtractDomain(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        try
        {
            // Handle URLs without protocol
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            var uri = new Uri(url);
            var host = uri.Host;

            // Remove "www." prefix
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                host = host[4..];

            return host.ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var sb = new System.Text.StringBuilder(1024);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
