using System.Diagnostics;
using System.IO;
using ScreenTimeTracker.Storage;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Provides category classification for applications.
/// Uses pre-populated cache with lazy loading for unknown apps.
/// Strategy: Cache once, use forever - zero repeated lookups.
/// </summary>
public class CategoryService
{
    private readonly UsageRepository _repository;
    
    // Runtime cache - pre-populated with defaults, learns new apps automatically
    // Thread-safe for concurrent access from tracking and UI threads
    private static readonly Dictionary<string, string> _appCategoryCache = 
        new(StringComparer.OrdinalIgnoreCase);
    
    private static readonly Dictionary<string, string> _websiteCategoryCache = 
        new(StringComparer.OrdinalIgnoreCase);
    
    private static readonly object _cacheLock = new();
    private static bool _cacheInitialized = false;
    
    // Default category mappings (Seed Data)
    private static readonly Dictionary<string, string> DefaultCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        // Productivity
        { "code.exe", "Productivity" },
        { "code", "Productivity" },
        { "devenv.exe", "Productivity" },
        { "devenv", "Productivity" },
        { "rider64.exe", "Productivity" },
        { "rider64", "Productivity" },
        { "idea64.exe", "Productivity" },
        { "idea64", "Productivity" },
        { "antigravity", "Productivity" },
        { "antigravity.exe", "Productivity" },
        { "notepad.exe", "Productivity" },
        { "notepad", "Productivity" },
        { "notepad++.exe", "Productivity" },
        { "notepad++", "Productivity" },
        { "sublime_text.exe", "Productivity" },
        { "sublime_text", "Productivity" },
        { "winword.exe", "Productivity" },
        { "winword", "Productivity" },
        { "excel.exe", "Productivity" },
        { "excel", "Productivity" },
        { "powerpnt.exe", "Productivity" },
        { "powerpnt", "Productivity" },
        { "onenote.exe", "Productivity" },
        { "onenote", "Productivity" },
        { "outlook.exe", "Productivity" },
        { "outlook", "Productivity" },
        { "notion.exe", "Productivity" },
        { "notion", "Productivity" },
        { "obsidian.exe", "Productivity" },
        { "obsidian", "Productivity" },
        { "terminal.exe", "Productivity" },
        { "terminal", "Productivity" },
        { "windowsterminal.exe", "Productivity" },
        { "windowsterminal", "Productivity" },
        { "powershell.exe", "Productivity" },
        { "powershell", "Productivity" },
        { "cmd.exe", "Productivity" },
        { "cmd", "Productivity" },
        
        // Browsers (Will be sub-categorized by URL, default to Other)
        { "chrome.exe", "Other" },
        { "chrome", "Other" },
        { "msedge.exe", "Other" },
        { "msedge", "Other" },
        { "firefox.exe", "Other" },
        { "firefox", "Other" },
        { "brave.exe", "Other" },
        { "brave", "Other" },
        { "opera.exe", "Other" },
        { "opera", "Other" },
        { "vivaldi.exe", "Other" },
        { "vivaldi", "Other" },
        
        // Social / Communication Apps
        { "slack.exe", "Social" },
        { "slack", "Social" },
        { "teams.exe", "Social" },
        { "teams", "Social" },
        { "zoom.exe", "Social" },
        { "zoom", "Social" },
        { "discord.exe", "Social" },
        { "discord", "Social" },
        { "skype.exe", "Social" },
        { "skype", "Social" },
        { "telegram.exe", "Social" },
        { "telegram", "Social" },
        { "whatsapp.exe", "Social" },
        { "whatsapp", "Social" },
        { "whatsapp.root", "Social" },
        { "signal.exe", "Social" },
        { "signal", "Social" },
        
        // Entertainment
        { "spotify.exe", "Entertainment" },
        { "spotify", "Entertainment" },
        { "vlc.exe", "Entertainment" },
        { "vlc", "Entertainment" },
        { "netflix.exe", "Entertainment" },
        { "netflix", "Entertainment" },
        { "primevideo.exe", "Entertainment" },
        { "primevideo", "Entertainment" },
        { "amazonmusic.exe", "Entertainment" },
        { "amazonmusic", "Entertainment" },
        { "itunes.exe", "Entertainment" },
        { "itunes", "Entertainment" },
        { "wmplayer.exe", "Entertainment" },
        { "wmplayer", "Entertainment" },
        { "movies & tv.exe", "Entertainment" },
        { "movies & tv", "Entertainment" },
        
        // Gaming (mapped to Entertainment)
        { "steam.exe", "Entertainment" },
        { "steam", "Entertainment" },
        { "steamwebhelper.exe", "Entertainment" },
        { "steamwebhelper", "Entertainment" },
        { "epicgameslauncher.exe", "Entertainment" },
        { "epicgameslauncher", "Entertainment" },
        { "origin.exe", "Entertainment" },
        { "origin", "Entertainment" },
        { "battle.net.exe", "Entertainment" },
        { "battle.net", "Entertainment" },
        { "riotclientservices.exe", "Entertainment" },
        { "riotclientservices", "Entertainment" },
        { "xboxapp.exe", "Entertainment" },
        { "xboxapp", "Entertainment" },
        { "gamingservices.exe", "Entertainment" },
        { "gamingservices", "Entertainment" },
        
        // System (mapped to Other)
        { "explorer.exe", "Other" },
        { "explorer", "Other" },
        { "searchui.exe", "Other" },
        { "searchui", "Other" },
        { "startmenuexperiencehost.exe", "Other" },
        { "startmenuexperiencehost", "Other" },
        { "shellexperiencehost.exe", "Other" },
        { "shellexperiencehost", "Other" },
        { "applicationframehost.exe", "Other" },
        { "applicationframehost", "Other" },
        { "lockapp.exe", "Other" },
        { "lockapp", "Other" },
    };

    // Website Domain -> Category mappings
    private static readonly Dictionary<string, string> WebsiteCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        // Entertainment
        { "youtube.com", "Entertainment" },
        { "netflix.com", "Entertainment" },
        { "twitch.tv", "Entertainment" },
        { "primevideo.com", "Entertainment" },
        { "disneyplus.com", "Entertainment" },
        
        // Social
        { "twitter.com", "Social" },
        { "x.com", "Social" },
        { "facebook.com", "Social" },
        { "instagram.com", "Social" },
        { "linkedin.com", "Social" },
        { "reddit.com", "Social" },
        { "tiktok.com", "Social" },
        { "pinterest.com", "Social" },
        { "mail.google.com", "Social" },
        { "outlook.live.com", "Social" },
        { "slack.com", "Social" },
        { "discord.com", "Social" },
        { "teams.microsoft.com", "Social" },
        { "zoom.us", "Social" },
        { "web.whatsapp.com", "Social" },
        
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
        
        // Other (Shopping, etc.)
        { "amazon.com", "Other" },
        { "amazon.in", "Other" },
        { "flipkart.com", "Other" },
        { "ebay.com", "Other" },
    };

    public CategoryService(UsageRepository repository)
    {
        _repository = repository;
        InitializeCacheOnce();
    }
    
    /// <summary>
    /// Pre-populates caches with default mappings and user overrides from database.
    /// Runs once at startup - achieves instant cache hits for all known apps.
    /// NOTE: Only user overrides are loaded from DB, not auto-learned categories.
    /// This ensures hardcoded defaults always apply unless user explicitly overrides.
    /// </summary>
    private void InitializeCacheOnce()
    {
        if (_cacheInitialized) return;
        
        lock (_cacheLock)
        {
            if (_cacheInitialized) return;
            
            // 1. Pre-populate with hardcoded defaults (instant, no I/O)
            foreach (var kvp in DefaultCategories)
            {
                _appCategoryCache[kvp.Key] = kvp.Value;
            }
            
            foreach (var kvp in WebsiteCategories)
            {
                _websiteCategoryCache[kvp.Key] = kvp.Value;
            }
            
            // 2. Load ONLY user overrides from database (not auto-learned)
            // This ensures our hardcoded defaults always take precedence
            try
            {
                var userOverrides = _repository.GetUserOverrideCategories();
                foreach (var kvp in userOverrides)
                {
                    // Only user overrides can override defaults
                    _appCategoryCache[kvp.Key] = kvp.Value;
                }
                
                Logger.Info($"CategoryService initialized: {_appCategoryCache.Count} apps (defaults + {userOverrides.Count} user overrides), {_websiteCategoryCache.Count} websites");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load user overrides: {ex.Message}");
            }
            
            _cacheInitialized = true;
        }
    }

    /// <summary>
    /// Gets the category for an application.
    /// Uses cache-first strategy: instant for known apps, learns new apps once.
    /// </summary>
    public string GetCategory(string appName)
    {
        if (string.IsNullOrEmpty(appName))
            return "Other";

        // Fast path: Check cache first (instant, no locks for reads)
        if (_appCategoryCache.TryGetValue(appName, out var cachedCategory))
            return cachedCategory;
        
        // Slow path: New app, resolve once and cache forever
        return ResolveAndCacheAppCategory(appName);
    }
    
    /// <summary>
    /// Resolves category for a new app and caches it permanently.
    /// Persists to database asynchronously (non-blocking).
    /// Called only once per unique app name - subsequent calls hit cache.
    /// </summary>
    private string ResolveAndCacheAppCategory(string appName)
    {
        lock (_cacheLock)
        {
            // Double-check after acquiring lock (another thread might have cached it)
            if (_appCategoryCache.TryGetValue(appName, out var existing))
                return existing;
            
            string category;
            
            // 1. Check user overrides in database
            var userOverride = _repository.GetAppCategory(appName);
            if (!string.IsNullOrEmpty(userOverride))
            {
                category = userOverride;
            }
            // 2. Try to match as browser-tracked website name
            else if (TryGetCategoryFromAppNameAsWebsite(appName, out var websiteCategory))
            {
                category = websiteCategory;
            }
            // 3. Default to "Other" for truly unknown apps
            else
            {
                category = "Other";
            }
            
            // Cache in memory immediately (instant for all future calls)
            _appCategoryCache[appName] = category;
            
            // Persist to database asynchronously (non-blocking, happens in background)
            // This ensures the category survives app restarts
            _ = Task.Run(() =>
            {
                try
                {
                    _repository.SetAppCategory(appName, category, isUserOverride: false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to persist category for {appName}: {ex.Message}");
                }
            });
            
            Logger.Debug($"Cached new app category: {appName} → {category} (persisting to DB)");
            
            return category;
        }
    }
    
    /// <summary>
    /// Tries to match app name to known website categories.
    /// Handles browser-tracked apps like "YouTube", "Netflix", etc.
    /// </summary>
    private static bool TryGetCategoryFromAppNameAsWebsite(string appName, out string category)
    {
        category = "Other";
        if (string.IsNullOrWhiteSpace(appName)) return false;
        
        // Known browser-tracked app names → categories (4 main categories only)
        var knownWebsiteApps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Entertainment
            { "YouTube", "Entertainment" },
            { "Netflix", "Entertainment" },
            { "Twitch", "Entertainment" },
            { "Prime Video", "Entertainment" },
            { "Disney+", "Entertainment" },
            { "Spotify", "Entertainment" },
            { "Hotstar", "Entertainment" },
            
            // Social
            { "Twitter", "Social" },
            { "Facebook", "Social" },
            { "Instagram", "Social" },
            { "LinkedIn", "Social" },
            { "Reddit", "Social" },
            { "TikTok", "Social" },
            { "WhatsApp", "Social" },
            { "WhatsApp Web", "Social" },
            { "Gmail", "Social" },
            { "Outlook", "Social" },
            { "Slack", "Social" },
            { "Discord", "Social" },
            { "Teams", "Social" },
            { "Zoom", "Social" },
            { "Google Meet", "Social" },
            
            // Productivity
            { "GitHub", "Productivity" },
            { "GitLab", "Productivity" },
            { "Stack Overflow", "Productivity" },
            { "Notion", "Productivity" },
            { "Figma", "Productivity" },
            { "Google Docs", "Productivity" },
            { "Google Sheets", "Productivity" },
            { "Trello", "Productivity" },
            { "Jira", "Productivity" },
            { "Claude", "Productivity" },
            { "ChatGPT", "Productivity" },
            { "Copilot", "Productivity" },
            { "Perfectinterview.ai", "Productivity" },
            
            // Other
            { "Amazon", "Other" },
            { "Flipkart", "Other" },
            { "eBay", "Other" },
        };
        
        // Direct match
        if (knownWebsiteApps.TryGetValue(appName, out var directMatch))
        {
            category = directMatch;
            return true;
        }
        
        // Partial match (e.g., "YouTube - Video Title" → "Entertainment")
        foreach (var kvp in knownWebsiteApps)
        {
            if (appName.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                category = kvp.Value;
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Gets the category for a website domain.
    /// Uses cache-first strategy with permanent caching.
    /// </summary>
    public string GetWebsiteCategory(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            return "Other";

        var cleanDomain = CleanDomain(domain);
        
        // Fast path: Check cache first
        if (_websiteCategoryCache.TryGetValue(cleanDomain, out var cachedCategory))
            return cachedCategory;
        
        // Slow path: Unknown website, cache as "Other"
        lock (_cacheLock)
        {
            if (!_websiteCategoryCache.TryGetValue(cleanDomain, out var existing))
            {
                _websiteCategoryCache[cleanDomain] = "Other";
                Logger.Debug($"Cached new website category: {cleanDomain} → Other");
            }
        }
        
        return "Other";
    }

    /// <summary>
    /// Checks if the given app is a browser.
    /// </summary>
    public bool IsBrowser(string appName)
    {
        if (string.IsNullOrEmpty(appName))
            return false;

        var browserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "chrome.exe", "msedge", "msedge.exe", 
            "firefox", "firefox.exe", "brave", "brave.exe",
            "opera", "opera.exe", "vivaldi", "vivaldi.exe"
        };
        
        return browserNames.Contains(appName);
    }

    /// <summary>
    /// Sets a user override for an app's category.
    /// Also updates the cache immediately.
    /// </summary>
    public void SetUserCategory(string appName, string category)
    {
        _repository.SetAppCategory(appName, category, isUserOverride: true);
        
        // Update cache immediately so UI reflects change
        lock (_cacheLock)
        {
            _appCategoryCache[appName] = category;
        }
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
}
