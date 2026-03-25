# MedApp — Software Design Document

> **Last updated:** 2026-03-19
> **Platform:** .NET 9 MAUI (Android-only, min SDK 26)
> **Package ID:** com.nextlevel.medapp

---

## 1. Architecture Overview

MedApp is a medication reminder app built with **.NET MAUI** using the **MVVM pattern**. It targets Android only (`net9.0-android`).

```
┌──────────────────────────────────────────────────┐
│                   Views (XAML)                    │
│  TodayPage · MedicationListPage · HistoryPage    │
│  AddEditMedicationPage · ReportPage · SettingsPage│
├──────────────────────────────────────────────────┤
│                  ViewModels                       │
│  ObservableObject + RelayCommand (MVVM Toolkit)  │
├──────────────────────────────────────────────────┤
│                   Services                        │
│  NotificationService · ReportService             │
│  IClock · INotificationDispatcher · IPreferences │
├──────────────────────────────────────────────────┤
│                  Data Layer                       │
│  DatabaseContext (SQLite) · Repositories          │
├──────────────────────────────────────────────────┤
│              Platform (Android)                   │
│  MainActivity · BootBroadcastReceiver            │
└──────────────────────────────────────────────────┘
```

### Key Libraries

| Library | Purpose |
|---------|---------|
| CommunityToolkit.Maui | UI helpers |
| CommunityToolkit.Mvvm | ObservableObject, RelayCommand |
| Plugin.LocalNotification v11 | Scheduled local notifications |
| QuestPDF | PDF report generation |
| sqlite-net-pcl + SQLitePCLRaw | Local SQLite database |

---

## 2. Project Structure

```
MedApp/
├── Models/
│   ├── Medication.cs          # Core medication entity
│   ├── Schedule.cs            # Daily alarm time per medication
│   ├── DoseLog.cs             # Per-dose status record
│   ├── DoseStatus.cs          # Enum: Pending, Taken, Missed, Snoozed, Skipped
│   ├── TodayDoseItem.cs       # View model for Today screen
│   ├── HistoryItem.cs         # View model for History screen
│   └── ReportData.cs          # Report summary + MedicationReportLine
├── Services/
│   ├── IClock.cs              # Time abstraction (interface)
│   ├── SystemClock.cs         # Production clock implementation
│   ├── INotificationDispatcher.cs  # Notification abstraction
│   ├── MauiNotificationDispatcher.cs # Plugin.LocalNotification wrapper
│   ├── IPreferencesProvider.cs     # Preferences abstraction
│   ├── MauiPreferencesProvider.cs  # MAUI Preferences wrapper
│   ├── NotificationService.cs # Scheduling, snooze, missed-dose logic
│   └── ReportService.cs       # Adherence reports + PDF export
├── ViewModels/
│   ├── TodayViewModel.cs      # Today's doses, take/snooze/skip actions
│   ├── MedicationListViewModel.cs  # Medication CRUD list
│   ├── AddEditMedicationViewModel.cs # Add/edit form with schedule times
│   ├── DoseHistoryViewModel.cs     # Date-filtered dose history
│   ├── ReportViewModel.cs     # Daily/weekly report generation
│   └── SettingsViewModel.cs   # Snooze duration, quiet hours, permissions
├── Views/
│   ├── TodayPage.xaml         # Home tab — today's dose list
│   ├── MedicationListPage.xaml # Medications tab — swipe-to-delete list
│   ├── AddEditMedicationPage.xaml # Modal — medication form + time pickers
│   ├── HistoryPage.xaml       # History tab — date picker + dose log
│   ├── ReportPage.xaml        # Reports tab — adherence stats + PDF export
│   └── SettingsPage.xaml      # Settings tab — snooze, quiet hours, permissions
├── Data/
│   ├── DatabaseContext.cs     # SQLite connection + table creation
│   ├── MedicationRepository.cs # Medication + Schedule CRUD
│   └── DoseLogRepository.cs   # DoseLog queries
├── Converters/
│   └── ValueConverters.cs     # IsZeroConverter, IsNotNullConverter
├── Platforms/Android/
│   ├── MainActivity.cs        # Notification channel + exact alarm permission
│   ├── MainApplication.cs     # MauiApplication entry point
│   ├── BootBroadcastReceiver.cs # Reschedule notifications on reboot
│   └── AndroidManifest.xml    # Permissions
├── App.xaml                   # Colors, converters, resource dictionary
├── App.xaml.cs                # Lifecycle, notification action handler, overdue timer
├── AppShell.xaml              # Tab navigation (5 tabs)
├── AppShell.xaml.cs           # Route registration
└── MauiProgram.cs             # DI container setup
```

---

## 3. Data Models

### 3.1 Medication

| Column | Type | Notes |
|--------|------|-------|
| Id | int | PK, auto-increment |
| Name | string | NotNull |
| Dosage | string | Optional (e.g., "500mg") |
| Notes | string | Optional |
| IsActive | bool | Default true; soft-delete flag |
| CreatedAt | DateTime | UTC, default UtcNow |

**Table:** `medications`

### 3.2 Schedule

| Column | Type | Notes |
|--------|------|-------|
| Id | int | PK, auto-increment |
| MedicationId | int | FK → Medication.Id (indexed) |
| TimeOfDayTicks | long | TimeSpan stored as ticks |
| IsActive | bool | Default true |
| StartDate | DateTime | Default Today |
| EndDate | DateTime? | Null = repeats indefinitely |

**Table:** `schedules`
**Computed property:** `TimeOfDay` (TimeSpan, ignored by SQLite)
**Relationship:** One Medication → many Schedules (e.g., 8 AM + 8 PM)

### 3.3 DoseLog

| Column | Type | Notes |
|--------|------|-------|
| Id | int | PK, auto-increment |
| ScheduleId | int | FK → Schedule.Id (indexed) |
| ScheduledAt | DateTime | UTC moment dose was due |
| Status | DoseStatus | Enum (int) |
| TakenAt | DateTime? | Set when user takes dose |
| Notes | string | Optional |

**Table:** `dose_logs`

### 3.4 DoseStatus Enum

```
Pending = 0, Taken = 1, Missed = 2, Snoozed = 3, Skipped = 4
```

### 3.5 View Models (non-persisted)

- **TodayDoseItem** — Combines Schedule + Medication + DoseLog for Today screen. Includes computed `TimeDisplay` ("h:mm tt"), `StatusText`, and `StatusColor`.
- **HistoryItem** — Combines DoseLog + medication name for History screen. Includes `TimeDisplay`, `DateDisplay` ("ddd, MMM d").
- **ReportData** — Aggregated adherence stats: TotalDoses, TakenCount, MissedCount, SkippedCount, AdherencePercent, per-medication breakdown via `List<MedicationReportLine>`.
- **MedicationSummary** — Inner class in MedicationListViewModel for list display: Id, Name, Dosage, TimesDisplay.

---

## 4. Services

### 4.1 NotificationService

The core business logic engine. All notification IDs are `ScheduleId * 1000` (snooze uses `+999` offset).

| Method | Purpose |
|--------|---------|
| `ScheduleAllAsync()` | Cancel all, reschedule all active medications. Called on app start + reboot. |
| `ScheduleForMedicationAsync(id)` | Reschedule notifications for one medication (after add/edit). |
| `CancelForMedicationAsync(id)` | Cancel notifications for a medication (on delete). |
| `SnoozeAsync(scheduleId, minutes)` | One-off snooze notification N minutes from now. |
| `DetectMissedDosesAsync()` | Scan today's schedules, create Missed logs for past unlogged doses. |
| `CheckOverdueDosesAsync()` | Mark Pending logs whose time has passed as Missed (called every 30s). |
| `HandleNotificationActionAsync(data, action)` | Process TAKE / SNOOZE / SKIP from notification buttons. |

**Internal logic:**
- `CalculateNextFireTime(TimeOfDay)` — If time hasn't passed → use today. If within 2-min grace → fire in 5 seconds. Otherwise → tomorrow.
- `ApplyQuietHours(notifyTime)` — If quiet hours enabled and time falls within quiet window, shift to quiet end time. Supports midnight wrapping.

### 4.2 ReportService

| Method | Purpose |
|--------|---------|
| `GetDailyReportAsync(date)` | Adherence stats for a single day. |
| `GetWeeklyReportAsync(weekStart)` | Adherence stats for 7 days. |
| `GeneratePdf(ReportData)` | Create PDF via QuestPDF, returns file path in cache directory. |

### 4.3 Abstractions

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| IClock | SystemClock | DateTime.Now / Today (testable) |
| INotificationDispatcher | MauiNotificationDispatcher | Plugin.LocalNotification wrapper |
| IPreferencesProvider | MauiPreferencesProvider | MAUI Preferences wrapper |

---

## 5. ViewModels

### 5.1 TodayViewModel

- Loads all active schedules, fetches DoseLog for each on today's date, builds `TodayDoseItem` list sorted by TimeOfDay.
- Commands: `LoadCommand`, `TakeCommand(scheduleId)`, `SnoozeCommand(scheduleId)`, `SkipCommand(scheduleId)`.
- Auto-refresh: 30-second timer calls `CheckOverdueDosesAsync` + reloads while page is visible.
- Summary: "X of Y doses taken" or "No medications scheduled today".

### 5.2 MedicationListViewModel

- Loads all active medications with formatted schedule times.
- Commands: `LoadCommand`, `AddCommand` (navigate), `EditCommand(id)` (navigate), `DeleteCommand(id)` (confirm + soft-delete).

### 5.3 AddEditMedicationViewModel

- Implements `IQueryAttributable` for route parameter `?id=` (edit mode).
- `ObservableCollection<TimeSpan> ScheduleTimes` — minimum 1 time required.
- Commands: `AddTimeCommand`, `RemoveTimeCommand(index)`, `SaveCommand`, `CancelCommand`.
- Save flow: Insert/update Medication → delete old Schedules → insert new Schedules → reschedule notifications → detect missed doses.

### 5.4 DoseHistoryViewModel

- Date-filtered dose log display.
- `SelectedDate` property triggers auto-reload on change.
- Maps DoseLog records to HistoryItem with medication names.

### 5.5 ReportViewModel

- Toggle between daily/weekly reports.
- Commands: `GenerateCommand`, `ExportPdfCommand` (share via native Share API).

### 5.6 SettingsViewModel

- Persisted settings: `SnoozeDuration` (5-60 min), `QuietHoursEnabled`, `QuietStart`, `QuietEnd`.
- Changing quiet hours triggers `ScheduleAllAsync()` to apply new timing.
- Permission check/request for Android 13+ notification permission.

---

## 6. Views & Navigation

### 6.1 Navigation Structure

5-tab `TabBar` in AppShell (no flyout):

| Tab | Route | Page | Icon |
|-----|-------|------|------|
| Today | `today` | TodayPage | — |
| Medications | `medications` | MedicationListPage | — |
| History | `history` | HistoryPage | — |
| Reports | `reports` | ReportPage | — |
| Settings | `settings` | SettingsPage | — |

**Modal route:** `AddEditMedicationPage` (registered in AppShell.xaml.cs)

### 6.2 Color Palette

| Name | Hex | Usage |
|------|-----|-------|
| Primary | #1565C0 | Headers, links, time display |
| PrimaryDark | #0D47A1 | — |
| PrimaryLight | #E3F2FD | — |
| Success | #2E7D32 | Taken status |
| Warning | #F57F17 | Snoozed status |
| Danger | #C62828 | Missed status |
| Muted | #757575 | Skipped status, secondary text |

---

## 7. Android Platform

### 7.1 Permissions (AndroidManifest.xml)

| Permission | Purpose |
|------------|---------|
| ACCESS_NETWORK_STATE | Plugin.LocalNotification internal |
| RECEIVE_BOOT_COMPLETED | Reschedule alarms after reboot |
| VIBRATE | Alarm vibration |
| WAKE_LOCK | Keep CPU awake during delivery |
| FOREGROUND_SERVICE | Background notification delivery |
| SCHEDULE_EXACT_ALARM | Exact timing (Android 12+) |
| USE_EXACT_ALARM | Guaranteed exact timing for health apps (Android 14+) |
| POST_NOTIFICATIONS | Required to post notifications (Android 13+) |

### 7.2 Notification Channel

Created in `MainActivity.OnCreate()`:

| Setting | Value |
|---------|-------|
| Channel ID | `medication_reminders` |
| Channel Name | Medication Reminders |
| Importance | High |
| Sound | Device default alarm |
| Audio Usage | Alarm |
| Vibration | Enabled |
| Bypass DND | true |
| Lockscreen | Public |

### 7.3 Boot Receiver

`BootBroadcastReceiver` listens for `ACTION_BOOT_COMPLETED`, resolves `NotificationService` from DI, and calls `ScheduleAllAsync()` to restore all medication alarms after device restart.

### 7.4 Exact Alarm Permission

`MainActivity.RequestExactAlarmPermission()` checks `AlarmManager.CanScheduleExactAlarms()` on Android 12+ (API 31+). If not granted, opens system settings page for the user to allow it.

---

## 8. Dependency Injection

Registered in `MauiProgram.cs`:

```
Singletons (shared for app lifetime):
  DatabaseContext
  MedicationRepository
  DoseLogRepository
  IClock → SystemClock
  INotificationDispatcher → MauiNotificationDispatcher
  IPreferencesProvider → MauiPreferencesProvider
  NotificationService
  ReportService

Transient (fresh per navigation):
  TodayViewModel, MedicationListViewModel, AddEditMedicationViewModel
  DoseHistoryViewModel, ReportViewModel, SettingsViewModel
  TodayPage, MedicationListPage, AddEditMedicationPage
  HistoryPage, ReportPage, SettingsPage
```

---

## 9. Key Flows

### 9.1 Adding a Medication

```
User fills form → SaveCommand validates
  → Insert Medication → Insert Schedule(s)
  → NotificationService.ScheduleForMedicationAsync()
    → CalculateNextFireTime() → ApplyQuietHours()
    → MauiNotificationDispatcher.ShowAsync()
      → Plugin.LocalNotification → Android AlarmManager
  → DetectMissedDosesAsync() (catch already-passed times)
```

### 9.2 Notification Fires

```
Android AlarmManager triggers at scheduled time
  → Plugin.LocalNotification posts notification
    → Channel: medication_reminders (alarm sound, vibration)
    → Actions: [Take] [Snooze] [Skip]
  → User taps action
    → App.OnNotificationActionTapped()
    → NotificationService.HandleNotificationActionAsync()
      → Update/insert DoseLog with status
      → If Snooze: schedule follow-up notification
```

### 9.3 Missed Dose Detection

```
Two mechanisms:
1. App launch/resume → DetectMissedDosesAsync()
   → For each past schedule with no log → create DoseLog(Missed)

2. Foreground timer (every 30s) → CheckOverdueDosesAsync()
   → For each past schedule with Pending log → update to Missed
   → For each past schedule with no log → create DoseLog(Missed)
```

### 9.4 Soft Delete

```
DeleteCommand → confirm dialog → SoftDeleteAsync()
  → Medication.IsActive = false
  → All schedules.IsActive = false
  → CancelForMedicationAsync() (remove notifications)
  → Historical DoseLogs preserved
```

---

## 10. Change Log


```
<!-- CHANGE: Delete a Daily schedule time -->
### Change: Delete a Daily schedule time
**Type:**  bugfix
**Priority:** high
**Status:** done

**Description:**
The user should be able to remove a time in the daily schedule for medicine needs to be take.  Example the user 2 times, but really only need to add one.

**Root cause:**
RemoveTime() had a guard `ScheduleTimes.Count > 1` that prevented removing the last schedule time.
SaveAsync() also required at least one schedule time, blocking the workflow needed to deactivate
a medication before deletion.

**Fix applied:**
- Removed `Count > 1` guard from RemoveTime() — users can now remove all schedule times.
- Removed "at least one schedule time" validation from SaveAsync() — a medication with zero
  schedules is valid (paused, no reminders).
- Added 4 unit tests in ScheduleRemovalTests covering remove-one, remove-all, remove-and-readd,
  and save-with-zero-schedules flows.

**Affected areas:**
- ViewModels/AddEditMedicationViewModel.cs (RemoveTime guard, SaveAsync validation)
- MedApp.Tests/Data/ScheduleRemovalTests.cs (new — 4 tests)
```

```
<!-- CHANGE: Delete Medicine -->
### Change: Delete Medicine
**Type:** feature
**Priority:** medium
**Status:** done

**Description:**
A interface that allows a user to delete a medicine that is not longer in use by the user.
The medicine can not have any time on the daily schedule to allow it to be removed.  If it has a time on the daily schedule tell the user can not be delete.

**Fix applied:**
- DeleteAsync() now checks for active schedules before allowing deletion.
- If schedules exist, displays alert: "Edit the medication and remove all schedule times before deleting."
- If no schedules, proceeds with confirmation dialog and soft-delete (dose history preserved).
- Added 5 unit tests in ScheduleRemovalTests covering has-schedules gate, no-schedules delete,
  full remove-then-delete workflow, and dose history preservation.

**Affected areas:**
- ViewModels/MedicationListViewModel.cs (DeleteAsync schedule check)
- MedApp.Tests/Data/ScheduleRemovalTests.cs (5 tests)
```



<!-- CHANGE: medicine-description -->
### Change: Medicine Name Not Showing After Save
**Type:** bugfix | ui
**Priority:** high
**Status:** done

**Description:**
The UI allows the user to enter the medicine name on Edit Medication screen. When you hit save
and exit back to the Medications screen the name does not show up. Add the name and verify with
a unit test.

**Root cause:**
Shell `OnAppearing` is unreliable on Android when popping back from a pushed route. The
MedicationListPage depended solely on `OnAppearing` to reload the medication list, so after
saving a medication, the list was not refreshed.

**Fix applied:**
- Added `MedicationSavedMessage` (WeakReferenceMessenger) sent after every add/edit save.
- `MedicationListViewModel` and `TodayViewModel` subscribe to this message and reload their data.
- Added 5 unit tests in `MedicationNamePersistenceTests` covering insert→reload, update→reload,
  multiple medications, save with schedules, and edit with schedule replacement.

**Affected areas:**
- Messages/MedicationSavedMessage.cs (new)
- ViewModels/AddEditMedicationViewModel.cs (sends message after save)
- ViewModels/MedicationListViewModel.cs (subscribes to message, reloads)
- ViewModels/TodayViewModel.cs (subscribes to message, reloads)
- MedApp.Tests/Data/MedicationNamePersistenceTests.cs (new — 5 tests)

---

<!-- Changes below this line -->
