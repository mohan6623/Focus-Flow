# ScreenTimeTracker - Core Application Behavior Plan

## Core Purpose
Build a lightweight, efficient screen time tracking application that helps users understand their digital habits and maintain focus, with multi-platform support across Windows, macOS, Android, iOS, and Linux.

## Core Behaviors & Features

### 1. Time Tracking (Foundation)
- ✅ **Foreground App Detection**: `ForegroundAppTracker.cs` monitors focused window
- ✅ **Browser Tab Tracking**: `BrowserTabResolver.cs` extracts URLs from Chrome/Edge/Firefox (Brave WIP)
- ✅ **Idle Detection**: `IdleDetector.cs` pauses tracking on inactivity
- ✅ **Lock State Awareness**: `LockStateMonitor.cs` pauses on lock/unlock
- ✅ **Session Management**: `SessionTracker.cs` groups usage into sessions
- ✅ **Persistence**: `UsageRepository.cs` stores to SQLite

### 2. Category Classification
- ✅ **Pre-populated Knowledge**: `CategoryService.cs` with 100+ apps pre-categorized
- ✅ **Learning System**: Caches new apps in SQLite
- ⏳ **User Overrides**: Not yet implemented (planned)
- ✅ **Performance Target**: Cache-first lookup achieved
- ✅ **Thread Safety**: Async persistence, non-blocking

### 3. Dashboard & Visualization
- ✅ **Real-time Stats**: `DashboardWindow.xaml` with 1-second refresh
- ⏳ **Category Breakdown**: Pie chart not yet implemented
- ✅ **Top Apps**: `AppUsageListContainer` with progress bars & percentages
- ✅ **Historical View**: Weekly bar chart with hourly drill-down
- ⏳ **Daily Average**: Not yet implemented
- ✅ **UI Responsiveness**: `AggregationService.cs` async background computation

### 4. Focus Mode
- ✅ **Distraction Blocking**: `AppBlockerService.cs` monitors blocked apps
- ✅ **Custom Duration**: `FocusDurationDialog.xaml` (25/50/custom mins)
- ✅ **Timer Display**: `FocusTimerWindow.xaml` always-on-top popup
- ✅ **App Blocking Overlay**: `BlockedAppOverlay.xaml` full-screen warning
- ⏳ **Configurable Blocklist**: Hardcoded in FocusService (UI config pending)

### 5. Resource Efficiency (Apple-like Performance)
- **CPU Usage**: Target <1% CPU during normal tracking
- **Memory Footprint**: Minimal RAM usage with efficient caching
- **Battery Impact**: Negligible battery drain on laptops
- **Startup Time**: Sub-second initialization with lazy loading
- **No Polling**: Event-driven architecture, no busy loops
- **Smart Updates**: Refresh only when data changes, not on fixed intervals

### 6. System Integration
- ✅ **System Tray**: `TrayIconManager.cs` with context menu
- ⏳ **Auto-start**: Not yet implemented
- ✅ **Native Feel**: WPF with Windows 11 styling
- ✅ **Theme Support**: `ThemeManager.cs` with Light/Dark toggle
- ✅ **No UAC Prompts**: Standard permissions only
- ✅ **Graceful Shutdown**: State saved on exit

### 7. Data Management
- ✅ **Local-First**: SQLite via `UsageRepository.cs`
- ✅ **Privacy**: All data on device
- ⏳ **Export/Import**: Not yet implemented
- ⏳ **Data Cleanup**: Not yet implemented
- ⏳ **Migration Support**: Not yet implemented

## Multi-Platform Vision (Phase 2+)

### 8. Cross-Device Sync
- **Single Account**: One user account across all devices
- **Server-Backed Categories**: Central category service for consistent classification
- **Platform-Agnostic Identifiers**: Map Windows exe ↔ Android package ↔ iOS bundle ↔ macOS app
- **Offline Capable**: Full functionality without internet, sync when available
- **Conflict Resolution**: Smart merging of tracking data from multiple devices
- **Web Dashboard**: Unified view of usage across all devices

### 9. Platform-Specific Implementations
- **Windows**: Current WPF app with ForegroundAppTracker
- **Android**: Kotlin app using UsageStatsManager API
- **iOS**: Swift app using Screen Time API (requires user permission)
- **macOS**: Swift/MAUI app using Accessibility API
- **Linux**: .NET app using X11/Wayland window tracking

## Non-Functional Requirements

### Performance Targets
- Category lookup: <0.1ms (cached), <5ms (DB query)
- Dashboard refresh: <50ms for 10,000+ sessions
- CPU usage: <1% during active tracking
- Memory: <100MB RAM footprint
- Startup: <1 second cold start

### User Experience Principles
- **Zero Configuration**: Works out of box with sensible defaults
- **Progressive Disclosure**: Simple by default, advanced features hidden
- **Non-Intrusive**: Background operation, minimal notifications
- **Trust & Privacy**: Clear about what's tracked, user owns their data
- **Predictable**: Consistent behavior, no surprises

### Code Quality Standards
- **Maintainability**: Clean separation of concerns (Services, Storage, Tracking, UI)
- **Testability**: Unit tests for core logic (category resolution, aggregation)
- **Documentation**: Code comments for complex algorithms
- **Error Handling**: Graceful degradation, never crash on bad data
- **Logging**: Diagnostic logs for debugging user issues

## Architecture Philosophy

### Current (Phase 1 - Windows)
- **Desktop-First**: Optimized for Windows with WPF
- **Local Storage**: SQLite for all data persistence
- **Cache-Heavy**: Pre-populate known apps, cache everything
- **Async-First**: All I/O operations async, never block UI thread

### Future (Phase 2+ - Multi-Platform)
- **Server-Backed**: REST API for category resolution and sync
- **Platform Clients**: Native apps sharing sync protocol
- **Hybrid Storage**: Local SQLite + cloud sync for redundancy
- **Eventual Consistency**: Accept temporary data inconsistency for performance

## Success Metrics
- **User Retention**: Daily active usage for insights
- **Performance**: <1% CPU, no user complaints about lag
- **Accuracy**: >95% correct category classification
- **Reliability**: <0.1% crash rate, data never lost
- **Multi-Platform**: Seamless experience across all devices (future)

## Current Status (February 2026)
✅ Phase 1 Complete: Windows app with efficient tracking, category caching, focus mode
✅ Performance: <1% CPU achieved (down from 10%+)
✅ Category System: 100+ pre-populated, learns new apps, persists to SQLite
✅ Dashboard: Real-time updates, async background computation
✅ Fixed: Antigravity app now correctly categorized as Productivity
✅ Fixed: WhatsApp desktop app (WhatsApp.Root) now tracked and categorized as Social
✅ Simplified: Only 4 main categories used (Productivity, Entertainment, Social, Other)
   - Communication apps → Social
   - Gaming apps → Entertainment  
   - Browsing apps → Other (or categorized by website if tracked)
   - System apps → Other
✅ Fixed: Auto-learned categories no longer override hardcoded defaults (only user overrides do)
✅ Fixed: Category legend now updates every 10 seconds and refreshes immediately on window activation
✅ Fixed: System processes excluded from tracking (ShellHost, ShellExperienceHost, SearchHost, etc.)
   - These Windows system components were incorrectly being tracked as user apps
   - 35+ system processes now automatically ignored
⏳ Phase 2 Pending: Server architecture design for multi-platform sync

## Implementation Roadmap (Ordered by Priority)

### 🔧 TIER 1: Core Tracking Fixes (Immediate)
*Foundation must be rock-solid before adding features.*

1. **Fix Brave Browser Tracking** ✅
   - [x] Debug UI Automation address bar detection
   - [x] Test with Debug Console created for this purpose
   - [x] Ensure consistent URL extraction across Chromium browsers

2. **Background vs Foreground Distinction** ✅
   - [x] Separate "Active Time" (focused window) from "Process Time" (running in background)
   - [x] Store both metrics separately in database
   - [x] Update UI to show both metrics

3. **Data Persistence Hardening**
   - [x] Fix TotalTime not persisting on restart (SetFromDatabase method added)
   - [ ] Verify streak data survives app restart
   - [ ] Audit all in-memory caches for persistence gaps

---

### 🎨 TIER 2: UI/UX Enhancements (Completed & Ongoing)

4. **Application Usage Widget** ✅
   - [x] Display top 8 apps with progress bars and percentages
   - [x] Real-time updates every second
   - [x] Placed below "Total Active Time" in dashboard

5. **Theme Switching (Light/Dark/System)** ✅
   - [x] Created ThemeManager service
   - [x] LightTheme.xaml and DarkTheme.xaml resource dictionaries
   - [x] Toggle button in dashboard header (cycles Light → Dark → System)
   - [x] Persist theme preference across restarts (via `user_settings` table)
   - [x] Added System theme option (follows Windows settings)

6. **Streak Display Widget** ✅
   - [x] Fire emoji with current streak count
   - [x] Weekly bubble visualization (Mon-Sun)
   - [x] Checkmarks for completed days
   - [x] Streak history persisted to `focus_session_history` table

---

### 🔥 TIER 3: Psychology-Based Productivity (Next Sprint)
*Behavioral science to maximize user engagement.*

7. **Advanced Streak System**
   - [ ] Hardened streak logic with database persistence
   - [ ] "Streak at risk" warning notifications
   - [ ] Milestone rewards (7-day, 30-day, 100-day unlockables)

8. **Focus Points & XP System**
   - [ ] Earn XP for every minute of focus session
   - [ ] Multiplier for "Deep Work" (60+ uninterrupted minutes)
   - [ ] Level progression (Beginner → Focus Master)

9. **Visual Progress Indicator (Forest-style)**
   - [ ] Visual element grows during focus session
   - [ ] Resets/dies if blocked app is opened
   - [ ] Integrates with existing App Blocker

---

### 🚫 TIER 4: Focus Mode Hardening

10. **Block Distracting Apps During Focus** ✅
    - [x] AppBlockerService monitors foreground apps
    - [x] BlockedAppOverlay displays when blocked app detected
    - [x] Integration with FocusService and FocusTimerWindow

11. **Progressive Friction System**
    - [ ] Level 1: Warning notification (soft nudge)
    - [ ] Level 2: Cooldown timer (30s delay before opening)
    - [ ] Level 3: Hard block (requires password/"Break Fee")

12. **Daily Time Limits**
    - [ ] Set per-app or per-category limits
    - [ ] Warning when approaching limit
    - [ ] Block after limit exceeded

13. **Scheduled Focus Modes**
    - [ ] Auto-start focus during work hours
    - [ ] Whitelist specific apps/websites per schedule

---

### 📊 TIER 5: Advanced Analytics

14. **24-Hour Timeline View**
    - [ ] Linear visualization of entire day
    - [ ] Show exact app switch timestamps
    - [ ] Clickable to drill down into details

15. **Weekly/Monthly Reports**
    - [ ] Comparative stats ("15% more focused than last week")
    - [ ] Insight generation ("40% of social media use is after 9 PM")
    - [ ] Exportable PDF/CSV reports

16. **App Categorization System**
    - [ ] Auto-categorize common apps
    - [ ] User can manually override categories
    - [ ] Filter dashboard by category

---

### 🌐 TIER 6: Multi-Platform & Sync (Future)

17. **Cloud Sync Architecture**
    - [ ] Design REST API for sync
    - [ ] Universal app identifier mapping (exe ↔ package ↔ bundle)
    - [ ] Conflict resolution for multi-device data

18. **Platform Clients**
    - [ ] Android (Kotlin + UsageStatsManager)
    - [ ] iOS (Swift + Screen Time API)
    - [ ] macOS (Swift/MAUI + Accessibility API)
    - [ ] Linux (.NET + X11/Wayland)

---

## Current Status (February 2026)
✅ Phase 1 Complete: Windows app with efficient tracking, category caching, focus mode
✅ Performance: <1% CPU achieved (down from 10%+)
✅ Category System: 100+ pre-populated, learns new apps, persists to SQLite
✅ Dashboard: Real-time updates, async background computation
✅ App Usage Widget: Top apps with progress bars
✅ Theme Switching: Light/Dark mode toggle
✅ App Blocking: Overlay during focus sessions
⏳ Next: Fix Brave tracking, then Streak/XP gamification
