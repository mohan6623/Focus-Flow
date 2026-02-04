# Bug & Resolution Log

This document tracks bugs, issues, and unexpected behaviors encountered during development, along with their solutions.

## 🟢 Resolved Issues

| Key | Issue Description | Resolution | Fixed Date |
|-----|-------------------|------------|------------|
| **Cat-01** | **Antigravity Categorization**<br>Antigravity app was not tracking correctly or categorized wrong. | Manually added to `CategoryService` as **Productivity**. | 2026-02-03 |
| **Cat-02** | **WhatsApp Desktop Tracking**<br>WhatsApp.Root was not being recognized. | Added mapping for `WhatsApp.Root` to **Social**. | 2026-02-03 |
| **Cat-03** | **Category Overrides**<br>Auto-learned categories were overriding hardcoded defaults. | Changed logic to prioritize hardcoded defaults over learning cache. Only user overrides now take precedence. | 2026-02-03 |
| **UI-01** | **Legend Refresh Lag**<br>Category legend wasn't updating frequently enough. | Implemented 10s timer and immediate refresh on `Window_Activated`. | 2026-02-03 |
| **Trk-01** | **System Noise**<br>Windows system processes (ShellHost, SearchHost) were cluttering stats. | Added filter to ignore 35+ known system processes in `ForegroundAppTracker`. | 2026-02-03 |
| **Dat-01** | **TotalTime Persistence**<br>Total active time was resetting on app restart. | Added `SetFromDatabase` method to restore state from SQLite on startup. | 2026-02-01 |
| **UI-02** | **Grid Column Error**<br>Build error in DashboardWindow.xaml.cs `Grid.Column`. | Replaced object initializer property with `Grid.SetColumn(element, 0)`. | 2026-01-27 |

| **Trk-02** | **Brave Browser Tracking**<br>UI Automation fails to reliably find the address bar in Brave. | Fixed by user. | 2026-02-03 |

| **Trk-03** | **Background vs Foreground**<br>Need to distinguish between app being focused vs just running. | Rectified. | 2026-02-03 |
| **UI-03** | **Theme Persistence**<br>Theme selection (Light/Dark) resets to default on app restart. | Added `user_settings` table, `ThemeManager` now persists to SQLite. Added System theme option. | 2026-02-03 |
| **Dat-02** | **Streak History Missing**<br>Weekly bubbles only showed last session date, not full history. | Created `focus_session_history` table. Each focus session now logs its date. Bubbles query actual history. | 2026-02-03 |

## 🔴 Pending / Known Issues

- [ ] **None currently tracked**
