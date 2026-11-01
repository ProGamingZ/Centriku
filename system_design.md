# Centriku — System Architecture & Technical Design Document

## 1. Real-World Business Workflow vs. Target Digitized Flow

### 1.1 Legacy Manual Workflow (Pain Points)
1. **Fragmented Spreadsheets:** Instructors maintain separate Excel files per class section for attendance, group tasks, recitation tracking, and term grades.
2. **Error-Prone Transmutation & FDA Math:** Calculating weighted category grades, Base-50/Base-60 NRFG transmutations, and Failure Due to Absences (FDA) contact-hour thresholds manually leads to formula drift across sheets.
3. **Redundant Student Data Entry:** At the start of every semester, instructors re-type or copy-paste student rosters across multiple class files, leading to inconsistent `StudentID` formatting and duplicate/orphan records.
4. **Manual Institutional Reporting:** Transferring computed grades and daily attendance logs into official university templates (`NwSSU-Class-Record.xlsx` and `ATTENDANCE.xlsx`) requires tedious cell-by-cell transcription.

### 1.2 Target Digitized Flow (Centriku Architecture)
1. **Centralized Master Directory:** Students are imported once via CSV/Excel (`BulkImportTab`) with automated column mapping, fallback auto-fill defaults, and 3-tier duplicate detection (by ID, Full Name, and Middle Name).
2. **Reusable Grading Policies:** Instructors define weighted grading templates (`PoliciesView`) with live 100%-weight validation, Passing Grade thresholds, and NRFG Base Value transmutation rules.
3. **Unified Class Gradebook:** Each `TeacherClass` links to the Master Directory via `ClassRoster` and provides 4 synchronized workspaces:
   * **Grades Tab:** Real-time weighted term/semester calculations with category visibility filters and bulk student transfer/unenroll.
   * **Groups Tab:** Group/Pair score + individual score clamping, leader appointment, and cross-assessment group cloning.
   * **Attendance Tab:** Contact-hour (`SessionHours`) roll calls with automated Equivalent or Independent FDA drop evaluation.
   * **Recitation Tab:** Randomized student/question picker with participation state tracking (`HasRecited`).
4. **Proactive Academic Telemetry:** The `DashboardView` aggregates all active/inactive classes, surfaces early-warning at-risk students and critical FDA/grade failures, and provides 1-click deep-linking straight to the student's row in the Gradebook.
5. **1-Click Official Template Export:** `ExcelExportService` injects live gradebook and attendance data directly into official `NwSSU-Class-Record.xlsx` and `ATTENDANCE.xlsx` workbooks.

---

## 2. Core Tech Stack Overview

| Layer | Technology / Library | Architectural Role |
| :--- | :--- | :--- |
| **Runtime & Language** | .NET 8.0 / C# 12 | Cross-platform desktop execution (Windows `win-x64`, macOS `osx-x64` / `osx-arm64`). |
| **UI Framework** | Avalonia UI (`11.x`) | XAML-based hardware-accelerated desktop UI with centralized Fluent theme tokens (`App.axaml`). |
| **Architectural Pattern** | MVVM (`CommunityToolkit.Mvvm`) | Source-generated `[ObservableProperty]` and `[RelayCommand]` bindings with strict View/ViewModel separation. |
| **Database Engine** | SQLite (`sqlite-net-pcl`) | Embedded, zero-configuration local relational storage (`centriku.db`) with async connection locking. |
| **Spreadsheet Ingestion** | `ExcelDataReader` | Streamed parsing of `.csv`, `.xls`, and `.xlsx` rosters on background threads. |
| **Spreadsheet Export** | `ClosedXML` | Manipulation and cell injection into pre-formatted `.xlsx` institutional templates. |
| **Automated Testing** | `xUnit` | Isolated temporary-file database unit and integration testing (`Centriku.Tests`). |

---

## 3. Complete Database Schema (Target Schema Version: v6)

Centriku uses `sqlite-net-pcl` with explicit schema migrations managed by `DatabaseMigrationService`.

### 3.1 Entity-Relationship Summary
* `GradingTemplate (1) ──< (N) GradingCategory`
* `GradingTemplate (1) ──< (N) TeacherClass`
* `Student (1) ──< (N) ClassRoster (N) >── (1) TeacherClass`
* `TeacherClass (1) ──< (N) Assessment ──< (N) Score >── (1) Student`
* `Assessment (1) ──< (N) AssessmentGroup ──< (N) AssessmentGroupMember >── (1) Student`
* `TeacherClass (1) ──< (N) AttendanceRecord >── (1) Student`
* `TeacherClass (1) ──< (N) RecitationQuestion`

### 3.2 Table Definitions

#### 1. `AppSettings` (Singleton Configuration Row: `Id = 1`)
| Column | Type | Constraints / Default | Purpose |
| :--- | :--- | :--- | :--- |
| `Id` | `int` | `PK` (`1`) | Singleton primary key |
| `DatabaseSchemaVersion` | `int` | Default `6` | Tracks executed migrations (`DatabaseMigrationService`) |
| `StudentIdColumnIndex` .. `EnrollmentStatusColumnIndex` | `int` | `0..9` or `-1` | Maps CSV/Excel column indices (`A..J`) to `Student` fields |
| `SkipFirstRow` | `bool` | Default `true` | Skips header row during bulk import |
| `DuplicateHandlingRule` | `string` | `"Update"` / `"Skip"` | Governs collision resolution during bulk import |
| `AutoCapitalizeNames` | `bool` | Default `true` | Converts imported names to Title Case |
| `SkipIncompleteRows` | `bool` | Default `true` | Flags rows missing First or Last Name |
| `DefaultGender`, `DefaultGradeLevel`, `DefaultProgram`, `DefaultSectionName`, `DefaultEnrollmentStatus` | `string` | Fallback strings | Auto-fill values for unmapped or blank cells |
| `DefaultExportFolderPath`, `FileNamingFormat` | `string` | Path / Pattern | Default output directory and token pattern for Excel exports |
| `ExportIncludeStudentId`, `ExportIncludeArchived` | `bool` | `true` / `false` | Privacy and archive toggles for exports |
| `ExportMissingScoreRule`, `ExportDecimalPrecision`, `ExportAttendanceDetail` | `string` | Formatting tokens | Controls score rounding and attendance export granularity |

#### 2. `Student` (Master Student Directory)
| Column | Type | Constraints / Indexes | Purpose |
| :--- | :--- | :--- | :--- |
| `StudentID` | `string` | `PK` (Supports `ON UPDATE CASCADE`) | Institutional Student ID (`e.g., 2024-0001`) |
| `FirstName`, `MiddleName`, `LastName`, `Suffix` | `string` | Trimmed strings | Student legal name components |
| `Gender` | `string` | `"Male"`, `"Female"`, `"None"` | Demographic metadata |
| `GradeYearLevel`, `Program`, `SectionName` | `string` | Filterable metadata | Academic cohort classification (`e.g., 3rd Year, BSIT, 3A`) |
| `EnrollmentStatus` | `string` | `"Regular"`, `"Irregular"`, `"Transferee"`, `"Dropped"` | Academic standing |
| `IsArchived` | `bool` | Default `false` | Separates active Master Roster from Archives |

#### 3. `GradingTemplate` & `GradingCategory` (Grading Policies)
| Table | Column | Type | Constraints / Indexes | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| `GradingTemplate` | `TemplateID` | `int` | `PK, AutoIncrement` | Unique policy identifier |
| `GradingTemplate` | `TemplateName` | `string` | Non-empty | Display name (`e.g., Standard Lab Policy`) |
| `GradingTemplate` | `PassingGrade` | `double` | Default `75.0` | Minimum passing threshold |
| `GradingTemplate` | `NrfgBaseValue` | `double` | Default `50.0` / `60.0` | Linear transmutation floor (`Base Value`) |
| `GradingCategory` | `CategoryID` | `int` | `PK, AutoIncrement` | Unique category identifier |
| `GradingCategory` | `TemplateID` | `int` | `FK -> GradingTemplate.TemplateID` | Parent grading template |
| `GradingCategory` | `Name` | `string` | Trimmed string | Category title (`Class Standing`, `MCO`, `Major Exam`) |
| `GradingCategory` | `Weight` | `double` | Sum per template = `100.0` | Percentage weight of category |
| `GradingCategory` | `SequenceOrder` | `int` | `0, 1, 2...` | Deterministic slot order for UI & NwSSU Excel export |

#### 4. `Classes` (`TeacherClass` Model)
| Column | Type | Constraints / Default | Purpose |
| :--- | :--- | :--- | :--- |
| `ClassID` | `int` | `PK, AutoIncrement` | Unique class identifier |
| `AcademicYear`, `Term`, `SubjectName`, `SectionLabel`, `Program`, `ProfessorName` | `string` | Metadata | Course and term identification |
| `GradingTemplateID` | `int` | `FK -> GradingTemplate.TemplateID` | Assigned grading policy |
| `ShowStudentId`, `ShowFirstName`, `ShowLastName`, `ShowFinalGrade`, `ShowTotalP`, `ShowTotalL`, `ShowTotalA`, `ShowTotalE` | `bool` | Default `true` | Persisted column visibility preferences per class |
| `IsActive` | `bool` | Default `true` | Active vs. Archived class state |
| `IsFdaEnabled` | `bool` | Default `false` | Enables Failure Due to Absences (FDA) engine |
| `FdaRuleType` | `string` | `"Equivalent"` / `"Independent"` | Determines whether late hours convert into absent hours |
| `FdaMaxAbsenceHours` | `double` | Default `10.0` | Absence contact-hour threshold before automatic drop |
| `FdaLatePenaltyRatio` | `double` | Default `0.33` | Conversion ratio (`Equivalent` mode) |
| `FdaMaxLateHours` | `double` | Default `6.0` | Separate late-hour threshold (`Independent` mode) |
| `FdaDropGrade` | `string` | Default `"5.0"` (`"5.0"` or `"DRP"`) | Grade output assigned when FDA threshold is breached |

#### 5. `ClassRoster`, `Assessment`, `Score`, `ScoreHistory`
| Table | Column | Type | Constraints / Indexes | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| `ClassRoster` | `RosterID` | `int` | `PK, AutoIncrement` | Enrollment row ID |
| `ClassRoster` | `ClassID`, `StudentID` | `int`, `string` | `FK -> Classes`, `FK -> Student` | Links student to class |
| `ClassRoster` | `HasRecited` | `bool` | Default `false` | Tracks recitation round completion |
| `Assessment` | `AssessmentID` | `int` | `PK, AutoIncrement` | Quiz/task column ID |
| `Assessment` | `ClassID` | `int` | `FK -> Classes.ClassID` | Parent class |
| `Assessment` | `Title`, `Category`, `GradingPeriod` | `string` | `"Midterm"` / `"Final"` | Task metadata and category binding |
| `Assessment` | `MaxScore` | `double` | `1..1000` | Maximum possible points |
| `Assessment` | `AssessmentType` | `string` | `"Solo"` / `"Group/Pair"` | Enables Groups Tab integration |
| `Score` | `ScoreID` | `int` | `PK, AutoIncrement` | Score cell ID |
| `Score` | `AssessmentID`, `StudentID` | `int`, `string` | `FK -> Assessment`, `FK -> Student` | Student score mapping |
| `Score` | `PointsEarned`, `IsExcused` | `double`, `bool` | `0 <= Points <= MaxScore` | Raw score or excused exemption |
| `ScoreHistory` | `LogID`, `ScoreID`, `AssessmentID`, `StudentID`, `ActionType`, `OldValue`, `Timestamp` | Audit types | `PK, AutoIncrement` | Historical score mutation log |

#### 6. `AssessmentGroup`, `AssessmentGroupMember`, `AttendanceRecords`, `RecitationQuestion`
| Table | Key Columns | Constraints / Indexes | Purpose |
| :--- | :--- | :--- | :--- |
| `AssessmentGroup` | `GroupID (PK)`, `AssessmentID`, `ClassID`, `GroupName`, `GroupScore` | `AutoIncrement` | Stores group name and shared base score |
| `AssessmentGroupMembers` | `MemberID (PK)`, `GroupID (Indexed)`, `AssessmentID (Indexed)`, `StudentID (Indexed)`, `IndividualScore`, `IsLeader` | `[Table("AssessmentGroupMembers")]` | Maps students to groups, individual adjustments, and leader status |
| `AttendanceRecords` | `RecordID (PK)`, `ClassID (Indexed)`, `StudentID (Indexed)`, `Date`, `Status`, `Reason`, `SessionHours` | `[Table("AttendanceRecords")]`, Default `1.5h` | Stores daily roll-call status (`P`, `L`, `A`, `E`) and session duration; uses `StudentID = "GHOST_DATE"` as a date-column anchor |
| `RecitationQuestion` | `QuestionID (PK)`, `ClassID`, `QuestionText`, `AnswerText`, `IsIncluded` | `AutoIncrement` | Question bank for randomized oral recitation |

---

## 4. Business Rules, Invariants, and Edge-Case Handling

1. **Grading Completeness Invariant:** A student's Term or Semester Average displays `"--"` (Incomplete) unless every `GradingCategory` in the template has at least one `Assessment` and the student has a recorded `Score` row for those assessments.
2. **Linear Transmutation Formula:** Raw percentage scores ($R \in [0, 100]$) are transmuted using the template's `NrfgBaseValue` ($B$):
   $$\text{Transmuted Grade} = \left(\frac{R}{100}\right) \times (100 - B) + B$$
3. **FDA Override Precedence:** If `IsFdaEnabled == true` and a student's absent/late hours meet or exceed `FdaMaxAbsenceHours` (or `FdaMaxLateHours` in `Independent` mode), the student's computed grade is immediately overridden with `FdaDropGrade` (`"5.0"` or `"DRP"`).
4. **NwSSU Template Slot Limits:** Per term (`Midterm` / `Final`), `SequenceOrder` slots enforce official spreadsheet column limits: Slot 0 (`Class Standing`) = max **10** assessments; Slot 1 (`MCO`) = max **5** assessments; Slot 2 (`Major Exam`) = max **1** assessment.
5. **Group Score Clamping:** In `Group/Pair` assessments, a member's total score is $\min(\text{MaxScore}, \max(0, \text{GroupScore} + \text{IndividualScore}))$, synchronized under a `SemaphoreSlim` lock (`_groupDbLock`) to prevent SQLite savepoint collisions.
6. **Referential Integrity & `ON UPDATE CASCADE`:**
   * Editing a student's `StudentID` in the Directory cascades the new ID across `ClassRoster`, `Score`, `AttendanceRecord`, `AssessmentGroupMember`, and `ScoreHistory`.
   * Permanently deleting a `Student`, `TeacherClass`, or `Assessment` cascades deletions to all dependent child records.
   * `AttendanceRecord` rows with `StudentID == "GHOST_DATE"` are protected from orphan purges so empty roll-call date columns persist.
7. **Non-Disruptive Hot Backups:** Database backups use `VACUUM INTO 'path'` so backups execute safely while the application connection remains live.
8. **Per-Cell Async Persistence Guard:** `ScoreCellViewModel` and `AttendanceCellViewModel` serialize rapid keystroke updates through a per-cell `SemaphoreSlim(1, 1)` so initial `ScoreID == 0` / `RecordID == 0` insertions complete and assign primary keys before subsequent keystrokes trigger `UpdateAsync`.
9. **Atomic SQL Recitation Reset & Diagnostics Serialization:** Resetting a class's recitation state executes a single atomic `UPDATE ClassRoster SET HasRecited = 0 WHERE ClassID = ?` query (`< 1ms`) while detaching `RotateTransform.Transitions` so the wheel resets instantaneously without reverse-spinning. `DatabaseManagementService` serializes telemetry, orphan purging, and `VACUUM` operations via a `SemaphoreSlim` lock.

---

## 5. Unresolved Business & Domain Questions

| # | Domain Area | Open Question | Current System Assumption |
| :--- | :--- | :--- | :--- |
| 1 | ** NwSSU Excel Export Settings** | Should `ExcelExportService` apply `ExportMissingScoreRule`, `ExportDecimalPrecision`, and `ExportIncludeArchived` from `ExportSettingsViewModel`, or must the official NwSSU `.xlsx` formulas remain untouched? | Currently, `ExportSettingsView` previews these rules, while `ExcelExportService` writes raw numeric scores so embedded Excel macros/formulas calculate normally. |
| 2 | **Excused Assessments (`IsExcused`)** | If a student is marked `IsExcused` on the *only* assessment in a category (e.g., Major Exam), how should category weight be redistributed? | Currently excluded from `catMax`, redistributing weight proportionally across remaining active categories. |
| 3 | **Cross-Term FDA Accumulation** | Are FDA absence hours accumulated across the entire semester, or reset between Midterm and Final terms? | Currently evaluated across all `AttendanceRecord` entries for the class across the semester. |
| 4 | **Excel Export Roster Cap** | What should happen if a class exceeds 100 students (`Row 12` to `Row 111` in `NwSSU-Class-Record.xlsx`)? | Currently capped at 100 students (`if (currentRow > 111) break;`) to prevent overwriting template footer blocks. |