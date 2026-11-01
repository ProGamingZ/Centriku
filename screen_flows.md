# Centriku — Screen Hierarchy, Route Map & User Flows

## 1. Route Map & Screen Hierarchy

Centriku uses a single-window shell (`MainWindow` / `MainWindowViewModel`) with cached top-level ViewModels and contextual drill-down views:

```text
MainWindow (MainWindowViewModel)
 ├── [Nav 1] DashboardView (DashboardViewModel)
 │     └── Deep-Link Action ──> Opens GradebookView (Target Class + Target Student / Tab)
 │
 ├── [Nav 2] MyClassesView (MyClassesViewModel)
 │     ├── Add / Edit Class Inline Drawer
 │     ├── Duplicate Class Confirmation Modal
 │     ├── Delete Class Confirmation Modal
 │     └── Open Class Action ──> GradebookView (GradebookViewModel)
 │           ├── [Tab 0] GradesTabView
 │           │     ├── Enroll Students Modal
 │           │     ├── Create / Edit Assessment Modal
 │           │     ├── Transfer Student Modal
 │           │     ├── Unenroll Student Modal
 │           │     ├── Delete Assessment Modal
 │           │     └── Export Excel Flyout
 │           ├── [Tab 1] GroupsTabView
 │           │     ├── Create Group Modal
 │           │     ├── Edit Group Modal
 │           │     ├── Copy Groups Modal
 │           │     └── Delete Group Modal
 │           ├── [Tab 2] AttendanceTabView
 │           │     ├── Log Roll Call Drawer
 │           │     ├── FDA Rules Configuration Drawer
 │           │     └── Student Attendance Detail Side Panel
 │           └── [Tab 3] RecitationTabView
 │                 ├── Random Student & Question Picker
 │                 └── Question Bank Manager
 │
 ├── [Nav 3] PoliciesView (PoliciesViewModel)
 │     ├── Template Editor Panel (Create / Edit Mode)
 │     ├── Saved Policies List
 │     └── Delete Policy Guard Modal (Blocks deletion if assigned to active classes)
 │
 ├── [Nav 4] DirectoryView (DirectoryViewModel)
 │     ├── [Tab 0] MasterRosterTab (Active Students, Sticky Multi-Select, Inline Edit)
 │     ├── [Tab 1] BulkImportTab (CSV/Excel Staging, Auto-Fill Preview, Re-Check)
 │     ├── [Tab 2] ArchivesTab (Archived Students, Sticky Multi-Select, Restore/Delete)
 │     ├── [Overlay] StudentProfileOverlay (Cross-Subject Performance & Attendance)
 │     └── [Modal] Shared Bulk Confirmation Modal (Archive / Restore / Delete)
 │
 └── [Nav 5] SettingsView (SettingsViewModel)
       ├── [Tab 0] SystemSettingsView (SystemSettingsViewModel)
       │     ├── Standard Mode (Teacher Health Card, 1-Click Optimize, Backup & Restore)
       │     └── Developer Diagnostics Mode (Telemetry, PRAGMA, Orphan Scan/Purge, VACUUM)
       ├── [Tab 1] ImportSettingsView (ImportSettingsViewModel)
       │     └── 3-Column Mapper (Columns A–J, Auto-Fill Defaults, Validation Rules, Live Preview)
       └── [Tab 2] ExportSettingsView (ExportSettingsViewModel)
             └── 3-Column Config (Folder/Naming, Privacy Toggles, Formatting, Live Sheet Previews)