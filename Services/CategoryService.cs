using System.Diagnostics;
using System.IO;
using ScreenTimeTracker.Storage;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Provides category classification for applications.
/// Uses a 3-tier lookup: User Overrides -> Default Mappings -> File Metadata.
/// </summary>
public class CategoryService
{
    private readonly UsageRepository _repository;
    
    // Default category mappings (Seed Data)
    private static readonly Dictionary<string, string> DefaultCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        // Productivity
        { "code.exe", "Productivity" },
        { "devenv.exe", "Productivity" },
        { "rider64.exe", "Productivity" },
        { "idea64.exe", "Productivity" },
        { "notepad.exe", "Productivity" },
        { "notepad++.exe", "Productivity" },
        { "sublime_text.exe", "Productivity" },
        { "winword.exe", "Productivity" },
        { "excel.exe", "Productivity" },
        { "powerpnt.exe", "Productivity" },
        { "onenote.exe", "Productivity" },
        { "outlook.exe", "Productivity" },
        { "notion.exe", "Productivity" },
        { "obsidian.exe", "Productivity" },
        { "terminal.exe", "Productivity" },
        { "windowsterminal.exe", "Productivity" },
        { "powershell.exe", "Productivity" },
        { "cmd.exe", "Productivity" },
        
        // Browsers (Will be sub-categorized by URL)
        { "chrome.exe", "Browsing" },
        { "msedge.exe", "Browsing" },
        { "firefox.exe", "Browsing" },
        { "brave.exe", "Browsing" },
        { "opera.exe", "Browsing" },
        { "vivaldi.exe", "Browsing" },
        
        // Communication
        { "slack.exe", "Communication" },
        { "teams.exe", "Communication" },
        { "zoom.exe", "Communication" },
        { "discord.exe", "Communication" },
        { "skype.exe", "Communication" },
        { "telegram.exe", "Communication" },
        { "whatsapp.exe", "Communication" },
        { "signal.exe", "Communication" },
        
        // Entertainment
        { "spotify.exe", "Entertainment" },
        { "vlc.exe", "Entertainment" },
        { "netflix.exe", "Entertainment" },
        { "primevideo.exe", "Entertainment" },
        { "amazonmusic.exe", "Entertainment" },
        { "itunes.exe", "Entertainment" },
        { "wmplayer.exe", "Entertainment" },
        { "movies & tv.exe", "Entertainment" },
        
        // Gaming
        { "steam.exe", "Gaming" },
        { "steamwebhelper.exe", "Gaming" },
        { "epicgameslauncher.exe", "Gaming" },
        { "origin.exe", "Gaming" },
        { "battle.net.exe", "Gaming" },
        { "riotclientservices.exe", "Gaming" },
        { "xboxapp.exe", "Gaming" },
        { "gamingservices.exe", "Gaming" },
        
        // System (usually excluded from tracking)
        { "explorer.exe", "System" },
        { "searchui.exe", "System" },
        { "startmenuexperiencehost.exe", "System" },
        { "shellexperiencehost.exe", "System" },
        { "applicationframehost.exe", "System" },
        { "lockapp.exe", "System" },
    };

    // Website Domain -> Category mappings
    private static readonly Dictionary<string, string> WebsiteCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        // Social Media
        { "youtube.com", "Entertainment" },
        { "netflix.com", "Entertainment" },
        { "twitch.tv", "Entertainment" },
        { "primevideo.com", "Entertainment" },
        { "disneyplus.com", "Entertainment" },
        
        { "twitter.com", "Social" },
        { "x.com", "Social" },
        { "facebook.com", "Social" },
        { "instagram.com", "Social" },
        { "linkedin.com", "Social" },
        { "reddit.com", "Social" },
        { "tiktok.com", "Social" },
        { "pinterest.com", "Social" },
        
        // Productivity
        { "github.com", "Productivity" },
        { "gitlab.com", "Productivity" },
        { "stackoverflow.com", "Productivity" },
        { "docs.google.com", "Productivity" },
        { "sheets.google.com", "Productivity" },
        { "notion.so", "Productivity" },
        { "figma.com", "Productivity" },
        { "trello.com", "Productivity" },
        { "asana.com", "Productivity" },
        { "jira.atlassian.com", "Productivity" },
        
        // Communication
        { "mail.google.com", "Communication" },
        { "outlook.live.com", "Communication" },
        { "slack.com", "Communication" },
        { "discord.com", "Communication" },
        { "teams.microsoft.com", "Communication" },
        { "zoom.us", "Communication" },
        
        // Shopping
        { "amazon.com", "Shopping" },
        { "amazon.in", "Shopping" },
        { "flipkart.com", "Shopping" },
        { "ebay.com", "Shopping" },
    };

    public CategoryService(UsageRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Gets the category for an application.
    /// </summary>
    public string GetCategory(string appName)
    {
        if (string.IsNullOrEmpty(appName))
            return "Other";

        // Tier 1: Check user overrides (from database)
        var userOverride = _repository.GetAppCategory(appName);
        if (!string.IsNullOrEmpty(userOverride))
            return userOverride;

        // Tier 2: Check default mappings
        if (DefaultCategories.TryGetValue(appName, out var defaultCategory))
            return defaultCategory;

        // Tier 3: Try to infer from file metadata
        var inferredCategory = InferCategoryFromMetadata(appName);
        if (!string.IsNullOrEmpty(inferredCategory))
            return inferredCategory;

        return "Other";
    }

    /// <summary>
    /// Gets the category for a website domain.
    /// </summary>
    public string GetWebsiteCategory(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            return "Browsing";

        // Clean up subdomain (e.g., "www.youtube.com" -> "youtube.com")
        var cleanDomain = CleanDomain(domain);

        if (WebsiteCategories.TryGetValue(cleanDomain, out var category))
            return category;

        return "Browsing"; // Default for unknown websites
    }

    /// <summary>
    /// Checks if the given app is a browser.
    /// </summary>
    public bool IsBrowser(string appName)
    {
        if (string.IsNullOrEmpty(appName))
            return false;

        return DefaultCategories.TryGetValue(appName, out var cat) && cat == "Browsing";
    }

    /// <summary>
    /// Sets a user override for an app's category.
    /// </summary>
    public void SetUserCategory(string appName, string category)
    {
        _repository.SetAppCategory(appName, category, isUserOverride: true);
    }

    /// <summary>
    /// Gets all available categories.
    /// </summary>
    public static IReadOnlyList<string> AvailableCategories { get; } = new[]
    {
        "Productivity",
        "Browsing",
        "Communication",
        "Entertainment",
        "Social",
        "Gaming",
        "Shopping",
        "System",
        "Other"
    };

    /// <summary>
    /// Gets the resource key for a category's brush (e.g., "CategoryProductivityBrush").
    /// Use with FindResource() in WPF code-behind.
    /// </summary>
    public static string GetCategoryBrushKey(string category)
    {
        return category switch
        {
            "Productivity" => "CategoryProductivityBrush",
            "Entertainment" => "CategoryEntertainmentBrush",
            "Communication" => "CategoryCommunicationBrush",
            "Gaming" => "CategoryGamingBrush",
            "Browsing" => "CategoryBrowsingBrush",
            "Social" => "CategorySocialBrush",
            "Shopping" => "CategoryShoppingBrush",
            "System" => "CategorySystemBrush",
            _ => "CategoryOtherBrush"
        };
    }

    /// <summary>
    /// Gets an emoji icon for a category.
    /// </summary>
    public static string GetCategoryIcon(string category)
    {
        return category switch
        {
            "Productivity" => "📋",
            "Entertainment" => "👾",
            "Communication" => "💬",
            "Gaming" => "🎮",
            "Browsing" => "🌐",
            "Social" => "👥",
            "Shopping" => "💳",
            "System" => "⚙️",
            "Finance" => "💰",
            _ => "📄"
        };
    }

    /// <summary>
    /// Gets the resource key for a category's color (e.g., "CategoryProductivityColor").
    /// </summary>
    public static string GetCategoryColorKey(string category)
    {
        return category switch
        {
            "Productivity" => "CategoryProductivityColor",
            "Entertainment" => "CategoryEntertainmentColor",
            "Communication" => "CategoryCommunicationColor",
            "Gaming" => "CategoryGamingColor",
            "Browsing" => "CategoryBrowsingColor",
            "Social" => "CategorySocialColor",
            "Shopping" => "CategoryShoppingColor",
            "System" => "CategorySystemColor",
            _ => "CategoryOtherColor"
        };
    }

    private static string CleanDomain(string domain)
    {
        // Remove "www." prefix
        if (domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            domain = domain[4..];

        // Remove port if present
        var colonIdx = domain.IndexOf(':');
        if (colonIdx > 0)
            domain = domain[..colonIdx];

        return domain.ToLowerInvariant();
    }

    private string? InferCategoryFromMetadata(string appName)
    {
        try
        {
            // Find the executable path
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(appName));
            if (processes.Length == 0)
                return null;

            var exePath = processes[0].MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return null;

            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            var companyName = versionInfo.CompanyName?.ToLowerInvariant() ?? "";

            // Infer category from company name
            if (companyName.Contains("microsoft"))
                return "Productivity";
            if (companyName.Contains("adobe"))
                return "Creative";
            if (companyName.Contains("valve") || companyName.Contains("steam") || 
                companyName.Contains("epic") || companyName.Contains("blizzard"))
                return "Gaming";
            if (companyName.Contains("google") || companyName.Contains("mozilla"))
                return "Browsing";

            return null;
        }
        catch
        {
            return null;
        }
    }
}
