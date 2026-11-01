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
 │           │     ├── Log Roll Call Modal
 │           │     ├── FDA Rules Configuration Modal
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
```

---

## 2. Detailed Breakdown Per Screen

### 2.1 `DashboardView` (`Nav: Dashboard`)
* **Purpose:** Executive academic overview and early-intervention command center.
* **Key Components:**
  * Academic Year, Term, and Program global filter bar.
  * 5 KPI Cards (`Total Enrolled`, `Total System Students`, `Active Classes`, `Inactive Classes`, `Early Warnings`).
  * Active Class Performance Cards (Pass / At-Risk / Fail distribution).
  * `Early Warnings (Needs Attention)` and `Critical Failures ({Count})` alert feeds.
* **Primary Actions:** Filter metrics by cohort; click any Class Card or Student Alert to deep-link directly into `GradebookView` with the target student highlighted.

### 2.2 `MyClassesView` (`Nav: My Classes`)
* **Purpose:** Course lifecycle management (create, edit, duplicate, archive, or delete classes).
* **Key Components:** Search bar, Year/Term filters, Collapsible `Add/Edit Class` drawer, `Active Classes` grid, and `Archived Classes` expander grid.
* **Primary Actions:**
  * `Create / Edit Class`: Links a subject/section to a `GradingTemplate`.
  * `Duplicate Class`: Clones class metadata, FDA rules, and active `ClassRoster` into a fresh section without copying old scores.
  * `Archive / Restore Toggle`: Moves classes between Active and Inactive states.
  * `Delete Class`: Cascade-deletes class, assessments, scores, groups, attendance, and recitations after modal confirmation.

### 2.3 `GradebookView` (`Drill-Down from My Classes or Dashboard`)
* **Purpose:** End-to-end classroom grading, group evaluation, attendance tracking, and official Excel generation.
* **Key Components & Actions by Sub-Tab:**
  * **`GradesTabView` (`Tab 0`):** Virtualized student-by-assessment `DataGrid` with dynamic `FrozenColumnCount`, Term View switcher (`Midterm`, `Final`, `Semester Average`), sticky-selection roster search, `DynamicGradesCounter`, `View Options` column visibility flyout (with coalesced category redraws), `Export Excel` flyout, and bulk `Enroll` (with `DynamicEnrollmentCounter` and sticky cohort filters), `Transfer`, and `Unenroll` modals.
  * **`GroupsTabView` (`Tab 1`):** Group assessment selector, `GroupCardViewModel` wrap grid with `GroupScore` and member `IndividualScore` inputs, Crown Leader toggle (`ToggleLeaderCommand`), `Unassigned Students` drawer with 1-click `+ Solo` group creation, empty-state guidance card, and `Copy Groups` modal.
  * **`AttendanceTabView` (`Tab 2`):** Date-by-student attendance grid (`P`, `L`, `A`, `E`) with dynamic `FrozenColumnCount`, `DynamicAttendanceCounter`, right-click `Attendance Details` side panel for logging excuse reasons/notes, `SessionHours` roll-call creator/editor (`SafeDoubleConverter`), and `FDA Configuration` modal (`Equivalent` vs. `Independent` rules).
  * **`RecitationTabView` (`Tab 3`):** Responsive 3-column workspace (`1* : 1.6* : 1*`) featuring a live status/question counter banner, scalable `Viewbox` student wheel displaying unique `FirstName + LastInitial` labels, empty-state guidance for `Remaining` and `Chosen` lists, question bank CRUD (`ManageQuestionsModal`), and atomic `< 1ms` class reset.

### 2.4 `PoliciesView` (`Nav: Policies`)
* **Purpose:** Define and maintain reusable grading formulas (`GradingTemplate` & `GradingCategory`).
* **Key Components:** Master-detail split layout with `Saved Templates` on the left and the full-width `Template Editor` on the right (Template Name, Passing Grade, NRFG Base Value, dynamic Category list, live `TotalWeight == 100%` validator).
* **Primary Actions:** Create policy, edit existing policy (cascading category renames to assessments), or delete policy (guarded by active-class dependency check).

### 2.5 `DirectoryView` (`Nav: Directory`)
* **Purpose:** Central student information system (SIS) and bulk roster ingestion.
* **Key Components & Actions by Sub-Tab:**
  * **`MasterRosterTab` (`Tab 0`):** Search bar, 4 cohort filters (`Year`, `Program`, `Section`, `Status`), `Columns ⚙️` visibility flyout, `Archive` bulk action button, `Add New Student` drawer, `DynamicStudentCounter`, and sticky-selection `DataGrid` with inline editing (`ON UPDATE CASCADE` for `StudentID`) and `View Full Profile`.
  * **`BulkImportTab` (`Tab 1`):** Shortcut to `Import Settings`, read-only `Auto-Fill Summary` pill bar, 10-column configuration preview, CSV/Excel file picker, editable `StagedStudents` review grid with semantic status badges (`New Student`, `Will Update`, `Will Skip`, `Error`), `DynamicImportCounter`, `Re-Check Data`, and `Confirm & Save`.
  * **`ArchivesTab` (`Tab 2`):** Archive search bar, `Restore` and `Delete` bulk toolbar buttons, `DynamicArchiveCounter`, and sticky-selection `DataGrid`.
  * **`StudentProfileOverlay`:** Full-screen overlay showing student demographics and cross-subject grades/attendance computed via `GradeCalculationService`.

### 2.6 `SettingsView` (`Nav: Settings`)
* **Purpose:** Database maintenance, developer diagnostics, and global import/export rules.
* **Key Components & Actions by Sub-Tab:**
  * **`SystemSettingsView` (`Tab 0`):** Dual-mode screen. **Standard Mode** provides health telemetry, 1-click `Optimize & Clean`, `Create Backup` (`VACUUM INTO`), and `Restore from Backup` (with `.failsafe.bak` rollback). **Developer Diagnostics Mode** provides table row counts, `PRAGMA integrity_check`, `Scan Ghost Records`, `Purge Ghost Records`, `Execute VACUUM`, and `Open Data Folder`.
  * **`ImportSettingsView` (`Tab 1`):** 3-column layout mapping Excel Columns `A–J`, setting fallback Auto-Fill defaults, configuring duplicate rules, and displaying a live preview row.
  * **`ExportSettingsView` (`Tab 2`):** 3-column layout configuring default export folder, file naming tokens, privacy toggles, and live Semestral/Attendance preview grids.

---

## 3. Multi-Step User Flows

### Flow 1: Onboarding & Bulk Student Import
1. User navigates to **Directory -> Bulk Import** (`SelectedTabIndex = 1`).
2. User reviews the **Current Column Configuration & Auto-Fill Defaults** strip. If columns don't match their spreadsheet, user clicks **Import Settings** (`OnNavigateToSettingsBulkImportTab`), adjusts Columns `A–J` and Auto-Fill defaults in `ImportSettingsView`, clicks **Save Import Settings**, and clicks **Open Bulk Import →** (`OnNavigateToDirectoryBulkImportTab`).
3. User clicks **Choose Data File** and selects a `.csv` or `.xlsx` roster.
4. `ProcessBulkImportAsync` parses rows on a background thread, applies Title Case and Auto-Fill defaults, checks duplicates against SQLite (by `StudentID`, then `LastName + FirstName + MiddleName`), and populates `StagedStudents` with `DynamicImportCounter`.
5. If any row shows an `Error` badge (e.g., missing ID), the user edits the cell directly inside the staging `DataGrid` and clicks **Re-Check Data** (`RecheckStagedDataCommand`).
6. User clicks **Confirm & Save to Directory** (`ConfirmBulkImportCommand`). The system re-validates, deduplicates in-memory batches, writes inserts/updates to SQLite, and refreshes the Master Roster.

### Flow 2: End-to-End Class Setup & Group Grading
1. User creates or verifies a 100%-weight policy in **Policies** (`PoliciesView`).
2. User navigates to **My Classes**, creates a class assigned to that policy, and clicks the class card to open **`GradebookView`**.
3. In **Grades Tab**, user clicks **Enroll Students**, filters available students by Program/Section (checked students remain sticky across filter changes), and clicks **Save Enrollment**.
4. User clicks **+ Add Assessment**, sets `Title = "Project 1"`, `Category = "Major Course Output"`, `Max Score = 100`, `Format = "Group/Pair"`, and saves.
5. User switches to **Groups Tab**, selects `"Project 1"`, clicks **+ Create Group** (or clicks **+ Solo** in the `Unassigned Students` drawer), assigns members, and appoints a group leader via the Crown icon (`ToggleLeaderCommand`).
6. User types a `Group Score` and optional `Individual Score` adjustments. `_groupDbLock` serializes database updates, clamps total scores to `[0, MaxScore]`, updates `GradebookRows` in memory, and recalculates final grades automatically.

### Flow 3: Cross-Section Sticky Selection & Bulk Archiving
1. User opens **Directory -> Master Roster**.
2. User filters by `Section = 3A` and checks 5 students (`IsSelected = true`).
3. User changes filter to `Section = 3B` or types a name into the search bar. Because `UpdateDisplayedStudents()` preserves rows where `s.IsSelected == true`, the 5 previously checked students remain visible alongside the new matches.
4. User checks additional students and clicks **Archive** in the toolbar.
5. `ConfirmModal` prompts with the exact count of selected students. Upon confirmation, all selected students are batch-updated (`IsArchived = true`), moved to `ArchivesTab`, and synced across all open gradebooks via `OnStudentRosterChanged`.