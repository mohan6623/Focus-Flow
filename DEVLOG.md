# FocuseFlow Development Log

This file tracks all changes made during development conversations.

---

## Session 1: January 25, 2026

### What Was Discussed
- User outlined full product vision for FocuseFlow - a cross-platform screen time tracker
- Priority features: Focus Timer, Background/Foreground tracking, App Categorization
- Cross-platform goal: Windows, Android, Linux
- App restriction system (warning → cooldown → block)

### What Was Implemented

#### ⭐ Focus Timer (Star Feature)
| Request | Implementation |
|---------|----------------|
| Focus button in dashboard | Added green "🎯 Focus" button to header |
| Always-on-top popup (Wispr Flow style) | Created `FocusTimerWindow.xaml` - minimal floating window |
| Editable duration before start | Created `FocusDurationDialog.xaml` with presets (25m, 45m, 60m) + custom input |
| Make popup circular | (Reverted) Back to rectangular design as requested |
| Custom Pause Icon | ✅ Clean 2-bar icon (no box) inside progress ring |
| Resize & Scale | ✅ Locked Aspect Ratio resizing (fixed shape), all-edge scaling enabled |
| Timer & Ring Alignment | ✅ Ring closer to timer, text centered and balanced |
| Tray-based timer option | ✅ Icon shows progress arc, tooltip shows countdown, right-click menu has pause/stop |
| Mini pause button | ✅ Inside progress ring, toggles ⏸/▶ |
| Click timer to toggle | ✅ Click timer text to switch between time left (white) and time spent (green) |
| Instant Drag | ✅ Dragging is now immediate (no long-press delay) |
| Double-tap to exit | ✅ Double-tap ANYWHERE (timer, circle, bg) to exit |
| **Bug Fixes** | ✅ Fixed "73.6h usage" bug (prevented data duplication) |

#### Dashboard Improvements
- Split weekly/hourly charts into separate stacked widgets
- Weekly chart always visible, hourly appears below when day clicked
- Gradient bars with glow effects for today's bar

#### Files Created
- `App/FocusTimerWindow.xaml` - Focus timer popup
- `App/FocusTimerWindow.xaml.cs` - Timer logic with gesture detection
- `App/FocusDurationDialog.xaml` - Duration selection dialog
- `App/FocusDurationDialog.xaml.cs` - Preset buttons + custom input
- `Services/FocusService.cs` - Focus session tracking service

#### Files Modified
- `App/DashboardWindow.xaml` - Added Focus button, split report widgets
- `App/DashboardWindow.xaml.cs` - Focus button handler, weekly/hourly separation

---

## Pending Tasks
- [ ] Tray icon shows timer during focus mode
- [ ] Background vs Foreground tracking
- [ ] App Categorization system
- [ ] Block distracting apps during focus
