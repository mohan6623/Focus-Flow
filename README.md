# Screen Time Tracker

A lightweight Windows desktop client that tracks application usage and screen time, similar to MIUI/HyperOS screen time functionality.

## Features

- **Event-driven tracking** - Uses Windows event hooks, not polling (near-zero CPU when idle)
- **MIUI-style metrics** - Tracks total time, session count, first/last usage per app
- **Screen lock detection** - Automatically pauses when screen is locked
- **Idle detection** - Pauses after 5 minutes of inactivity (configurable)
- **Offline storage** - SQLite database with batched writes
- **System tray** - Runs silently with tray icon and context menu
- **Single instance** - Prevents multiple instances from running

## Performance Targets

| Metric | Target |
|--------|--------|
| RAM Usage | < 30 MB |
| CPU (Idle) | ~0% |
| Disk I/O | Batched every 5 min |

## Project Structure

```
ScreenTimeTracker/
├── App/
│   ├── Program.cs              # Entry point
│   ├── AppBootstrap.cs         # Dependency wiring
│   └── TrayIconManager.cs      # System tray icon
├── Core/
│   ├── TrackingState.cs        # Current active app state
│   ├── UsageSession.cs         # Single session model
│   └── AppUsageStats.cs        # Aggregated stats per app
├── Tracking/
│   ├── ForegroundAppTracker.cs # Windows event hook tracking
│   ├── SessionTracker.cs       # Session lifecycle management
│   ├── LockStateMonitor.cs     # Screen lock detection
│   └── IdleDetector.cs         # User idle detection
├── Storage/
│   ├── UsageRepository.cs      # SQLite persistence
│   └── Models/
│       └── StoredUsage.cs      # Database model
├── Services/
│   ├── AggregationService.cs   # Daily stats aggregation
│   └── SyncService.cs          # Backend sync (future)
├── System/
│   ├── ProcessResolver.cs      # PID to process name
│   └── WindowInfoProvider.cs   # Window info via P/Invoke
├── Utilities/
│   ├── Logger.cs               # Serilog wrapper
│   └── TimeProvider.cs         # Time abstraction
├── Config/
│   └── AppSettings.cs          # Configuration management
└── appsettings.json            # Default settings
```

## Requirements

- Windows 10 or Windows 11
- .NET 8.0 SDK

## Build

```powershell
# Build
dotnet build

# Run
dotnet run

# Publish (single file)
dotnet publish -c Release -r win-x64 --self-contained false
```

## Configuration

Settings are stored in `%LocalAppData%\ScreenTimeTracker\settings.json`:

```json
{
  "AutoStartTracking": true,
  "StartWithWindows": true,
  "IdleTimeoutMinutes": 5,
  "PersistIntervalMinutes": 5,
  "MinSessionDurationSeconds": 1,
  "ShowTrayIcon": true
}
```

## Data Storage

Usage data is stored in `%LocalAppData%\ScreenTimeTracker\usage.db` (SQLite).

Logs are in `%LocalAppData%\ScreenTimeTracker\Logs\`.

## Architecture

```
Windows Events (Foreground, Lock, Input)
           ↓
    Tracking Layer (Event-driven)
           ↓
    Session Tracker (State machine)
           ↓
    Aggregation Service (In-memory with periodic flush)
           ↓
    SQLite Repository (Batched writes)
```

## Future Enhancements

- [ ] Backend API sync
- [ ] Statistics viewer UI
- [ ] Settings dialog
- [ ] App categories
- [ ] Focus mode / App limits
- [ ] Weekly/monthly reports

## License

MIT
