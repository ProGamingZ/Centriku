using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.ViewModels.Modals;

namespace Centriku.ViewModels
{
    public partial class DirectoryViewModel 
    {
        public partial class StagedStudent : ObservableObject
        {
            public Centriku.Models.Student DbModel { get; set; } = new();
            [ObservableProperty] public partial bool IsDuplicate { get; set; }
            [ObservableProperty] public partial bool IsError { get; set; } 
            [ObservableProperty] public partial string ImportStatus { get; set; } = string.Empty;
            [ObservableProperty] public partial string StatusColor { get; set; } = "#FFFFFF";
            public StagedStudent(Centriku.Models.Student student) { DbModel = student; }
        }
            [ObservableProperty] public partial ObservableCollection<Centriku.ViewModels.Settings.PreviewStudentRow> CurrentSettingsPreview { get; set; } = new();
            [ObservableProperty] public partial string Col1H { get; set; } = ""; [ObservableProperty] public partial string Col2H { get; set; } = "";
            [ObservableProperty] public partial string Col3H { get; set; } = ""; [ObservableProperty] public partial string Col4H { get; set; } = "";
            [ObservableProperty] public partial string Col5H { get; set; } = ""; [ObservableProperty] public partial string Col6H { get; set; } = "";
            [ObservableProperty] public partial string Col7H { get; set; } = ""; [ObservableProperty] public partial string Col8H { get; set; } = "";
            [ObservableProperty] public partial string Col9H { get; set; } = ""; [ObservableProperty] public partial string Col10H { get; set; } = "";

            public async Task LoadImportSettingsPreviewAsync()
            {
                var db = _dbService.GetConnection();
                var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();

                string[] headers = new string[10];
                for (int i = 0; i < 10; i++) headers[i] = "Ignore";

                void SetHeader(int index, string name) { if (index >= 0 && index < 10) headers[index] = name; }
                
                SetHeader(settings.StudentIdColumnIndex, "Student ID"); SetHeader(settings.LastNameColumnIndex, "Last Name");
                SetHeader(settings.FirstNameColumnIndex, "First Name"); SetHeader(settings.MiddleNameColumnIndex, "Middle Name");
                SetHeader(settings.SuffixColumnIndex, "Suffix"); SetHeader(settings.GenderColumnIndex, "Gender");
                SetHeader(settings.GradeLevelColumnIndex, "Year"); SetHeader(settings.ProgramColumnIndex, "Program");
                SetHeader(settings.SectionNameColumnIndex, "Section"); SetHeader(settings.EnrollmentStatusColumnIndex, "Status");

                Col1H = headers[0]; Col2H = headers[1]; Col3H = headers[2]; Col4H = headers[3]; Col5H = headers[4];
                Col6H = headers[5]; Col7H = headers[6]; Col8H = headers[7]; Col9H = headers[8]; Col10H = headers[9];

                string GetPreview(string field)
                {
                    if (field == "Ignore") return "[ Ignored ]";
                    if (field == "Gender") return settings.DefaultGender == "None" ? "--" : settings.DefaultGender;
                    if (field == "Year") return settings.DefaultGradeLevel;
                    if (field == "Program") return settings.DefaultProgram;
                    if (field == "Section") return settings.DefaultSectionName;
                    if (field == "Status") return settings.DefaultEnrollmentStatus;
                    return "--"; 
                }

                var singleRow = new Centriku.ViewModels.Settings.PreviewStudentRow {
                    Col1Text = GetPreview(Col1H), Col2Text = GetPreview(Col2H), Col3Text = GetPreview(Col3H),
                    Col4Text = GetPreview(Col4H), Col5Text = GetPreview(Col5H), Col6Text = GetPreview(Col6H),
                    Col7Text = GetPreview(Col7H), Col8Text = GetPreview(Col8H), Col9Text = GetPreview(Col9H),
                    Col10Text = GetPreview(Col10H)
                };
                
                CurrentSettingsPreview = new ObservableCollection<Centriku.ViewModels.Settings.PreviewStudentRow> { singleRow };
            }
        private List<StudentRowViewModel> _allStudents = [];
        private List<StudentRowViewModel> _allArchivedStudents = [];
        [ObservableProperty] public partial ObservableCollection<StudentRowViewModel> DisplayedStudents { get; set; } = new();
        [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;

        [ObservableProperty] public partial ObservableCollection<string> AvailableYears { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<string> AvailablePrograms { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<string> AvailableSections { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<string> AvailableStatuses { get; set; } = new();
        
        [ObservableProperty] public partial string SelectedYearFilter { get; set; } = "All Years";
        [ObservableProperty] public partial string SelectedProgramFilter { get; set; } = "All Programs";
        [ObservableProperty] public partial string SelectedSectionFilter { get; set; } = "All Sections";
        [ObservableProperty] public partial string SelectedStatusFilter { get; set; } = "All Statuses";
        
        [ObservableProperty] public partial string DynamicStudentCounter { get; set; } = "Showing: 0 students";
        [ObservableProperty] public partial string DynamicArchiveCounter { get; set; } = "Showing: 0 students";

        // Master & Archive Select-All Checkboxes
        [ObservableProperty] public partial bool IsAllMasterSelected { get; set; } = false;
        partial void OnIsAllMasterSelectedChanged(bool value)
        {
            foreach (var row in DisplayedStudents) row.IsSelected = value;
        }

        [ObservableProperty] public partial bool IsAllArchiveSelected { get; set; } = false;
        partial void OnIsAllArchiveSelectedChanged(bool value)
        {
            foreach (var row in DisplayedArchivedStudents) row.IsSelected = value;
        }

        // Confirmation Modal State (Shared by Archive, Restore, and Delete)
        [ObservableProperty] public partial bool IsConfirmModalOpen { get; set; } = false;
        [ObservableProperty] public partial ConfirmDeleteModalViewModel? ConfirmModal { get; set; }
        [ObservableProperty] public partial string ConfirmModalButtonText { get; set; } = "Confirm";
        [ObservableProperty] public partial string ConfirmModalButtonColor { get; set; } = "#EF4444";

        // Toast Feedback State
        public Action<string>? ShowToastMessage { get; set; }
        [ObservableProperty] public partial string ToastMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsToastVisible { get; set; } = false;

        private async void TriggerToast(string message)
        {
            ToastMessage = message;
            IsToastVisible = true;
            ShowToastMessage?.Invoke(message);
            await Task.Delay(3000);
            if (ToastMessage == message) IsToastVisible = false;
        }


        partial void OnSelectedYearFilterChanged(string value) => UpdateDisplayedStudents();
        partial void OnSelectedProgramFilterChanged(string value) => UpdateDisplayedStudents();
        partial void OnSelectedSectionFilterChanged(string value) => UpdateDisplayedStudents();
        partial void OnSelectedStatusFilterChanged(string value) => UpdateDisplayedStudents();

        [ObservableProperty] public partial ObservableCollection<StudentRowViewModel> DisplayedArchivedStudents { get; set; } = new();
        [ObservableProperty] public partial string ArchiveSearchQuery { get; set; } = string.Empty;

        // Archive Column Visibility
        [ObservableProperty] public partial bool ShowArchiveStudentIdColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveLastNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveFirstNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveMiddleNameColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveSuffixColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveGenderColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveGradeYearLevelColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveProgramColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveSectionNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveEnrollmentStatusColumn { get; set; } = true;

        // Master Column Visibility
        [ObservableProperty] public partial bool ShowStudentIdColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowLastNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowFirstNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowMiddleNameColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowSuffixColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowGenderColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowGradeYearLevelColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowProgramColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowSectionNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowEnrollmentStatusColumn { get; set; } = true;

        // Add Student Form Properties
        [ObservableProperty] public partial bool IsAddingStudent { get; set; } = false;
        [ObservableProperty] public partial string NewStudentId { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentFirstName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentMiddleName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentLastName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentSuffix { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentGender { get; set; } = "Male";
        [ObservableProperty] public partial string NewStudentGradeYearLevel { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentProgram { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentSectionName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentEnrollmentStatus { get; set; } = "Regular";

        public async Task LoadStudentsAsync()
        {
            var db = _dbService.GetConnection();
            await db.CreateTableAsync<Centriku.Models.Student>();
            var rawStudents = await db.Table<Centriku.Models.Student>().ToListAsync();
            _allStudents = rawStudents.Where(s => !s.IsArchived).Select(s => new StudentRowViewModel(s)).ToList();
            _allArchivedStudents = rawStudents.Where(s => s.IsArchived).Select(s => new StudentRowViewModel(s)).ToList();

            var years = _allStudents.Select(s => s.GradeYearLevel).Where(y => !string.IsNullOrWhiteSpace(y)).Distinct().OrderBy(y => y).ToList();
            AvailableYears.Clear(); AvailableYears.Add("All Years");
            foreach (var y in years) AvailableYears.Add(y!);
            if (!AvailableYears.Contains(SelectedYearFilter)) SelectedYearFilter = "All Years";

            var programs = _allStudents.Select(s => s.Program).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().OrderBy(p => p).ToList();
            AvailablePrograms.Clear(); AvailablePrograms.Add("All Programs");
            foreach (var p in programs) AvailablePrograms.Add(p!);
            if (!AvailablePrograms.Contains(SelectedProgramFilter)) SelectedProgramFilter = "All Programs";

            var sections = _allStudents.Select(s => s.SectionName).Where(sec => !string.IsNullOrWhiteSpace(sec)).Distinct().OrderBy(sec => sec).ToList();
            AvailableSections.Clear(); AvailableSections.Add("All Sections");
            foreach (var s in sections) AvailableSections.Add(s!);
            if (!AvailableSections.Contains(SelectedSectionFilter)) SelectedSectionFilter = "All Sections";

            var statuses = _allStudents.Select(s => s.EnrollmentStatus).Where(es => !string.IsNullOrWhiteSpace(es)).Distinct().OrderBy(es => es).ToList();
            AvailableStatuses.Clear(); AvailableStatuses.Add("All Statuses");
            foreach (var st in statuses) AvailableStatuses.Add(st!);
            if (!AvailableStatuses.Contains(SelectedStatusFilter)) SelectedStatusFilter = "All Statuses";

            UpdateDisplayedStudents();
            UpdateDisplayedArchivedStudents();
        }

        private async Task SaveStudentAsync()
        {
            if (string.IsNullOrWhiteSpace(NewStudentId) || string.IsNullOrWhiteSpace(NewStudentLastName)) return;

            string trimmedId = NewStudentId.Trim();
            var db = _dbService.GetConnection();

            // PREVENT SILENT OVERWRITE: Do not overwrite an existing student if the ID is already taken
            var existing = await db.Table<Centriku.Models.Student>().Where(s => s.StudentID == trimmedId).FirstOrDefaultAsync();
            if (existing != null) return;

            var newStudent = new Centriku.Models.Student
            {
                StudentID = trimmedId, FirstName = NewStudentFirstName.Trim(), MiddleName = NewStudentMiddleName.Trim(), LastName = NewStudentLastName.Trim(),
                Suffix = NewStudentSuffix.Trim(), Gender = NewStudentGender, GradeYearLevel = NewStudentGradeYearLevel.Trim(),
                Program = NewStudentProgram.Trim(), SectionName = NewStudentSectionName.Trim(), EnrollmentStatus = NewStudentEnrollmentStatus, IsArchived = false
            };
            await db.InsertAsync(newStudent);
            
            NewStudentId = string.Empty; NewStudentFirstName = string.Empty; NewStudentMiddleName = string.Empty;
            NewStudentLastName = string.Empty; NewStudentSuffix = string.Empty; NewStudentGradeYearLevel = string.Empty;
            NewStudentProgram = string.Empty; NewStudentSectionName = string.Empty; NewStudentEnrollmentStatus = "Regular";
            
            IsAddingStudent = false;
            await LoadStudentsAsync();
            OnStudentRosterChanged?.Invoke();
        }

        private async Task EditOrSaveStudentAsync(StudentRowViewModel row)
        {
            if (row == null) return;
            
            // 1. ENTER EDIT MODE: Snapshot the current StudentID before the user changes it
            if (!row.IsEditing) 
            {
                row.OriginalStudentID = row.StudentID;
                row.IsEditing = true;
                return;
            }

            // 2. SAVE MODE: Validate that the new ID and Last Name aren't blank
            string oldId = (row.OriginalStudentID ?? string.Empty).Trim();
            string newId = (row.StudentID ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(newId) || string.IsNullOrWhiteSpace(row.LastName))
            {
                // Revert to original ID if the user accidentally cleared the box
                row.StudentID = oldId;
                row.IsEditing = false;
                await LoadStudentsAsync();
                return;
            }

            row.StudentID = newId;
            row.FirstName = row.FirstName.Trim();
            row.MiddleName = row.MiddleName.Trim();
            row.LastName = row.LastName.Trim();
            row.Suffix = row.Suffix.Trim();
            row.GradeYearLevel = row.GradeYearLevel.Trim();
            row.Program = row.Program.Trim();
            row.SectionName = row.SectionName.Trim();

            var db = _dbService.GetConnection();

            // CASE A: StudentID was NOT changed -> Standard update
            if (string.Equals(oldId, newId, StringComparison.Ordinal))
            {
                await db.UpdateAsync(row.DbModel);
            }
            // CASE B: StudentID WAS changed (e.g., "TEMP-01" -> "2026-0001") -> ON UPDATE CASCADE
            else
            {
                // Collision Guard: Ensure the new ID doesn't already belong to another student
                var existingConflict = await db.Table<Centriku.Models.Student>()
                    .Where(s => s.StudentID == newId)
                    .FirstOrDefaultAsync();

                if (existingConflict != null && !string.Equals(oldId, newId, StringComparison.OrdinalIgnoreCase))
                {
                    // Revert ID to prevent overwriting another student's record
                    row.StudentID = oldId;
                    row.IsEditing = false;
                    await LoadStudentsAsync();
                    return;
                }

                // 1. Insert the student record under the new StudentID
                await db.InsertOrReplaceAsync(row.DbModel);

                // 2. Cascade the new StudentID across all child tables
                var rosters = await db.Table<Centriku.Models.ClassRoster>().Where(r => r.StudentID == oldId).ToListAsync();
                foreach (var r in rosters) r.StudentID = newId;
                if (rosters.Count > 0) await db.UpdateAllAsync(rosters, runInTransaction: false);

                var scores = await db.Table<Centriku.Models.Score>().Where(s => s.StudentID == oldId).ToListAsync();
                foreach (var s in scores) s.StudentID = newId;
                if (scores.Count > 0) await db.UpdateAllAsync(scores, runInTransaction: false);

                var attendance = await db.Table<Centriku.Models.AttendanceRecord>().Where(a => a.StudentID == oldId).ToListAsync();
                foreach (var a in attendance) a.StudentID = newId;
                if (attendance.Count > 0) await db.UpdateAllAsync(attendance, runInTransaction: false);

                var groupMembers = await db.Table<Centriku.Models.AssessmentGroupMember>().Where(m => m.StudentID == oldId).ToListAsync();
                foreach (var gm in groupMembers) gm.StudentID = newId;
                if (groupMembers.Count > 0) await db.UpdateAllAsync(groupMembers, runInTransaction: false);

                try
                {
                    var history = await db.Table<Centriku.Models.ScoreHistory>().Where(h => h.StudentID == oldId).ToListAsync();
                    foreach (var h in history) h.StudentID = newId;
                    if (history.Count > 0) await db.UpdateAllAsync(history, runInTransaction: false);
                }
                catch { /* Safe ignore if ScoreHistory table isn't initialized in lightweight unit tests */ }

                // 3. Delete the old temporary Student record so no duplicate/ghost remains
                if (!string.IsNullOrEmpty(oldId))
                {
                    await db.DeleteAsync(new Centriku.Models.Student { StudentID = oldId });
                }
            }

            row.OriginalStudentID = newId;
            row.IsEditing = false;
            await LoadStudentsAsync();
            OnStudentRosterChanged?.Invoke();
        }

        private async Task ArchiveStudentAsync(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = _dbService.GetConnection();
            row.DbModel.IsArchived = true; await db.UpdateAsync(row.DbModel); await LoadStudentsAsync(); OnStudentRosterChanged?.Invoke();
        }

        private async Task RestoreStudentAsync(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = _dbService.GetConnection();
            row.DbModel.IsArchived = false; await db.UpdateAsync(row.DbModel); await LoadStudentsAsync(); OnStudentRosterChanged?.Invoke();
        }

        private async Task DeleteStudentAsync(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = _dbService.GetConnection();
            string targetId = row.StudentID;

            // CASCADE DELETE: Wipe all child records so no Ghost/Orphan records remain in the system!
            var rosters = await db.Table<Centriku.Models.ClassRoster>().Where(r => r.StudentID == targetId).ToListAsync();
            var scores = await db.Table<Centriku.Models.Score>().Where(s => s.StudentID == targetId).ToListAsync();
            var attendance = await db.Table<Centriku.Models.AttendanceRecord>().Where(a => a.StudentID == targetId).ToListAsync();
            var groupMembers = await db.Table<Centriku.Models.AssessmentGroupMember>().Where(m => m.StudentID == targetId).ToListAsync();

            foreach (var r in rosters) await db.DeleteAsync(r);
            foreach (var s in scores) await db.DeleteAsync(s);
            foreach (var a in attendance) await db.DeleteAsync(a);
            foreach (var gm in groupMembers) await db.DeleteAsync(gm);

            await db.DeleteAsync(row.DbModel);
            await LoadStudentsAsync();
            OnStudentRosterChanged?.Invoke();
        }

        [RelayCommand]
        public void PromptBulkArchive()
        {
            var selected = _allStudents.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                TriggerToast("Please select at least one student first.");
                return;
            }

            ConfirmModalButtonText = "Archive";
            ConfirmModalButtonColor = "#F59E0B";
            ConfirmModal = new ConfirmDeleteModalViewModel(
                "Archive Student(s)?",
                $"Are you sure you want to move {selected.Count} selected student(s) to the Archives?",
                async () =>
                {
                    var db = _dbService.GetConnection();
                    foreach (var row in selected)
                    {
                        row.DbModel.IsArchived = true;
                        row.IsSelected = false;
                    }
                    await db.UpdateAllAsync(selected.Select(r => r.DbModel), runInTransaction: false);

                    IsAllMasterSelected = false;
                    IsConfirmModalOpen = false;
                    ConfirmModal = null;

                    await LoadStudentsAsync();
                    OnStudentRosterChanged?.Invoke();
                    TriggerToast($"Archived {selected.Count} student(s).");
                },
                () => { IsConfirmModalOpen = false; ConfirmModal = null; }
            );
            IsConfirmModalOpen = true;
        }
        [RelayCommand]
        public void PromptBulkRestore()
        {
            var selected = _allArchivedStudents.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                TriggerToast("Please select at least one student first.");
                return;
            }

            ConfirmModalButtonText = "Restore";
            ConfirmModalButtonColor = "#10B981";
            ConfirmModal = new ConfirmDeleteModalViewModel(
                "Restore Student(s)?",
                $"Are you sure you want to restore {selected.Count} selected student(s) to the active Master Roster?",
                async () =>
                {
                    var db = _dbService.GetConnection();
                    foreach (var row in selected)
                    {
                        row.DbModel.IsArchived = false;
                        row.IsSelected = false;
                    }
                    await db.UpdateAllAsync(selected.Select(r => r.DbModel), runInTransaction: false);

                    IsAllArchiveSelected = false;
                    IsConfirmModalOpen = false;
                    ConfirmModal = null;

                    await LoadStudentsAsync();
                    OnStudentRosterChanged?.Invoke();
                    TriggerToast($"Restored {selected.Count} student(s).");
                },
                () => { IsConfirmModalOpen = false; ConfirmModal = null; }
            );
            IsConfirmModalOpen = true;
        }
        [RelayCommand]
        public void PromptBulkDelete()
        {
            var selected = _allArchivedStudents.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                TriggerToast("Please select at least one student first.");
                return;
            }

            ConfirmModalButtonText = "Delete Permanently";
            ConfirmModalButtonColor = "#EF4444";
            ConfirmModal = new ConfirmDeleteModalViewModel(
                "Permanently Delete Student(s)?",
                $"Are you sure you want to permanently delete {selected.Count} selected student(s)?\n\nThis will permanently erase their profile, class enrollments, scores, group memberships, and attendance records.",
                async () =>
                {
                    var db = _dbService.GetConnection();
                    var targetIds = selected.Select(s => s.StudentID).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();

                    var rosters = (await db.Table<Centriku.Models.ClassRoster>().ToListAsync()).Where(r => targetIds.Contains(r.StudentID)).ToList();
                    var scores = (await db.Table<Centriku.Models.Score>().ToListAsync()).Where(s => s.StudentID != null && targetIds.Contains(s.StudentID)).ToList();
                    var attendance = (await db.Table<Centriku.Models.AttendanceRecord>().ToListAsync()).Where(a => a.StudentID != null && targetIds.Contains(a.StudentID)).ToList();
                    var groupMembers = (await db.Table<Centriku.Models.AssessmentGroupMember>().ToListAsync()).Where(m => targetIds.Contains(m.StudentID)).ToList();

                    foreach (var r in rosters) await db.DeleteAsync(r);
                    foreach (var s in scores) await db.DeleteAsync(s);
                    foreach (var a in attendance) await db.DeleteAsync(a);
                    foreach (var gm in groupMembers) await db.DeleteAsync(gm);
                    foreach (var row in selected) await db.DeleteAsync(row.DbModel);

                    IsAllArchiveSelected = false;
                    IsConfirmModalOpen = false;
                    ConfirmModal = null;

                    await LoadStudentsAsync();
                    OnStudentRosterChanged?.Invoke();
                    TriggerToast($"Permanently deleted {selected.Count} student(s).");
                },
                () => { IsConfirmModalOpen = false; ConfirmModal = null; }
            );
            IsConfirmModalOpen = true;
        }



        [ObservableProperty] public partial ObservableCollection<StagedStudent> StagedStudents { get; set; } = new();
        [ObservableProperty] public partial bool HasStagedStudents { get; set; } = false;
        [ObservableProperty] public partial bool HasImportError { get; set; } = false;
        [ObservableProperty] public partial string ImportSummaryMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsLoading { get; set; } = false;
        
        [RelayCommand] public static void NavigateToSettings() => OnNavigateToSettingsBulkImportTab?.Invoke();
        public static event System.Action? OnNavigateToSettingsBulkImportTab;

        public async Task ProcessBulkImportAsync(string filePath)
        {
            try
            {
                StagedStudents.Clear(); HasImportError = false; HasStagedStudents = false; IsLoading = true; ImportSummaryMessage = string.Empty;
                var db = _dbService.GetConnection();
                var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();
                var existingStudents = await db.Table<Centriku.Models.Student>().ToListAsync();
                var existingIds = existingStudents.Select(s => s.StudentID).ToHashSet();

                var stagedList = await Task.Run(() =>
                {
                    var tempList = new List<StagedStudent>();
                    string extension = Path.GetExtension(filePath).ToLower();
                    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                    void ProcessRow(string[] cols)
                    {
                        if (cols.Length == 0) return;
                        var parsed = ParseStudentRow(cols, settings);
                        var staged = new StagedStudent(parsed);
                        bool isError = false; string errorMsg = "";

                        if (string.IsNullOrWhiteSpace(parsed.StudentID)) { isError = true; errorMsg = "Error: Missing Student ID"; }
                        else if (settings.SkipIncompleteRows)
                        {
                            if (settings.LastNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.LastName)) { isError = true; errorMsg = "Error: Missing Last Name"; }
                            else if (settings.FirstNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.FirstName)) { isError = true; errorMsg = "Error: Missing First Name"; }
                        }

                        if (isError) { staged.IsError = true; staged.ImportStatus = errorMsg; staged.StatusColor = "#EF4444"; }
                        else
                        {
                            bool isDup = false;
                            
                            // 1. Check by ID first
                            if (!string.IsNullOrWhiteSpace(parsed.StudentID) && existingIds.Contains(parsed.StudentID)) 
                            {
                                isDup = true;
                            } 
                            else 
                            {
                                // 2. Check by Full Name (Last + First)
                                var nameMatch = existingStudents.FirstOrDefault(e => 
                                    string.Equals(e.LastName, parsed.LastName, StringComparison.OrdinalIgnoreCase) && 
                                    string.Equals(e.FirstName, parsed.FirstName, StringComparison.OrdinalIgnoreCase));
                                
                                if (nameMatch != null) 
                                {
                                    // 3. Middle Name check: True if either is empty, OR if they match exactly
                                    if (string.IsNullOrWhiteSpace(parsed.MiddleName) || 
                                        string.IsNullOrWhiteSpace(nameMatch.MiddleName) || 
                                        string.Equals(nameMatch.MiddleName, parsed.MiddleName, StringComparison.OrdinalIgnoreCase)) 
                                    {
                                        isDup = true;
                                        // Adopt the DB's StudentID so it overwrites the correct existing record
                                        staged.DbModel.StudentID = nameMatch.StudentID; 
                                    }
                                }
                            }

                            if (isDup)
                            {
                                staged.IsDuplicate = true;
                                if (settings.DuplicateHandlingRule == "Skip") { staged.ImportStatus = "Will Skip"; staged.StatusColor = "#6B7280"; } 
                                else { staged.ImportStatus = "Will Update"; staged.StatusColor = "#EAB308"; } 
                            }
                            else { staged.ImportStatus = "New Student"; staged.StatusColor = "#22C55E"; }
                        }
                        tempList.Add(staged);
                    }

                    if (extension == ".csv")
                    {
                        var lines = File.ReadAllLines(filePath);
                        int startRow = settings.SkipFirstRow ? 1 : 0; 
                        for (int i = startRow; i < lines.Length; i++) ProcessRow(lines[i].Split(','));
                    }
                    else if (extension == ".xlsx" || extension == ".xls")
                    {
                        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
                        using var reader = ExcelReaderFactory.CreateReader(stream);
                        if (settings.SkipFirstRow) reader.Read(); 
                        while (reader.Read())
                        {
                            var cols = new string[reader.FieldCount];
                            for (int i = 0; i < reader.FieldCount; i++) cols[i] = reader.GetValue(i)?.ToString() ?? string.Empty;
                            ProcessRow(cols);
                        }
                    }
                    return tempList;
                });

                if (!stagedList.Any()) { HasImportError = true; ImportSummaryMessage = "Error: The file appears to be empty or unreadable."; }
                else
                {
                    StagedStudents = new ObservableCollection<StagedStudent>(stagedList);
                    HasStagedStudents = true;
                    int errors = stagedList.Count(s => s.IsError);
                    string dupMsg = stagedList.Any(s => s.IsDuplicate) ? $" ({stagedList.Count(s => s.IsDuplicate)} duplicates)" : "";
                    if (errors > 0) ImportSummaryMessage = $"Warning: Found {stagedList.Count} rows, but {errors} have missing information.";
                    else ImportSummaryMessage = $"Success! Read {stagedList.Count} valid students{dupMsg}.";
                }
            }
            catch (Exception ex) { HasImportError = true; ImportSummaryMessage = $"File Error: {ex.Message}"; }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        public async Task RecheckStagedDataAsync()
        {
            if (!StagedStudents.Any()) return;
            IsLoading = true; ImportSummaryMessage = "Re-evaluating data...";
            var db = _dbService.GetConnection();
            var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();
            var existingStudents = await db.Table<Centriku.Models.Student>().ToListAsync();
            var existingIds = existingStudents.Select(s => s.StudentID).ToHashSet();
            int errorCount = 0;

            foreach (var staged in StagedStudents)
            {
                var parsed = staged.DbModel;
                bool isError = false; string errorMsg = "";

                if (string.IsNullOrWhiteSpace(parsed.StudentID)) { isError = true; errorMsg = "Error: Missing Student ID"; }
                else if (settings.SkipIncompleteRows)
                {
                    if (settings.LastNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.LastName)) { isError = true; errorMsg = "Error: Missing Last Name"; }
                    else if (settings.FirstNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.FirstName)) { isError = true; errorMsg = "Error: Missing First Name"; }
                }

                if (isError) { staged.IsError = true; staged.ImportStatus = errorMsg; staged.StatusColor = "#EF4444"; errorCount++; }
                else
                {
                    staged.IsError = false; 
                    
                    bool isDup = false;
                    
                    // 1. Check by ID first
                    if (!string.IsNullOrWhiteSpace(parsed.StudentID) && existingIds.Contains(parsed.StudentID)) 
                    {
                        isDup = true;
                    } 
                    else 
                    {
                        // 2. Check by Full Name (Last + First)
                        var nameMatch = existingStudents.FirstOrDefault(e => 
                            string.Equals(e.LastName, parsed.LastName, StringComparison.OrdinalIgnoreCase) && 
                            string.Equals(e.FirstName, parsed.FirstName, StringComparison.OrdinalIgnoreCase));
                        
                        if (nameMatch != null) 
                        {
                            // 3. Middle Name check
                            if (string.IsNullOrWhiteSpace(parsed.MiddleName) || 
                                string.IsNullOrWhiteSpace(nameMatch.MiddleName) || 
                                string.Equals(nameMatch.MiddleName, parsed.MiddleName, StringComparison.OrdinalIgnoreCase)) 
                            {
                                isDup = true;
                                staged.DbModel.StudentID = nameMatch.StudentID; 
                            }
                        }
                    }

                    if (isDup)
                    {
                        staged.IsDuplicate = true;
                        if (settings.DuplicateHandlingRule == "Skip") { staged.ImportStatus = "Will Skip"; staged.StatusColor = "#6B7280"; } 
                        else { staged.ImportStatus = "Will Update"; staged.StatusColor = "#EAB308"; } 
                    }
                    else { staged.IsDuplicate = false; staged.ImportStatus = "New Student"; staged.StatusColor = "#22C55E"; }
                }
            }
            int total = StagedStudents.Count; int dupes = StagedStudents.Count(s => s.IsDuplicate);
            string dupMsg = dupes > 0 ? $" ({dupes} duplicates)" : "";
            if (errorCount > 0) ImportSummaryMessage = $"Warning: Found {total} rows, but {errorCount} still have missing information.";
            else ImportSummaryMessage = $"Success! All {total} students are valid{dupMsg}. Ready to save.";
            IsLoading = false;
        }

        [RelayCommand]
        public async Task ConfirmBulkImportAsync()
        {
            if (!StagedStudents.Any()) return;

            // Always re-evaluate staged data in case the user edited cells inline without clicking "Re-Check Data"
            await RecheckStagedDataAsync();

            var db = _dbService.GetConnection();
            var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();
            
            // Deduplicate by StudentID within the staged batch itself so InsertAllAsync never hits a PRIMARY KEY collision
            var toInsert = new Dictionary<string, Centriku.Models.Student>(StringComparer.OrdinalIgnoreCase);
            var toUpdate = new Dictionary<string, Centriku.Models.Student>(StringComparer.OrdinalIgnoreCase);

            foreach (var staged in StagedStudents)
            {
                if (staged.IsError || string.IsNullOrWhiteSpace(staged.DbModel.StudentID)) continue;
                
                if (staged.IsDuplicate)
                {
                    if (settings.DuplicateHandlingRule == "Update")
                        toUpdate[staged.DbModel.StudentID] = staged.DbModel;
                }
                else
                {
                    toInsert[staged.DbModel.StudentID] = staged.DbModel;
                }
            }

            if (toInsert.Count != 0) await db.InsertAllAsync(toInsert.Values, runInTransaction: false);
            if (toUpdate.Count != 0) await db.UpdateAllAsync(toUpdate.Values, runInTransaction: false);
            
            CancelBulkImport();
            await LoadStudentsAsync();
            OnStudentRosterChanged?.Invoke();
        }
        
        [RelayCommand]
        public void CancelBulkImport() { StagedStudents.Clear(); HasStagedStudents = false; HasImportError = false; ImportSummaryMessage = string.Empty; }
        
        private static Centriku.Models.Student ParseStudentRow(string[] cols, Centriku.Models.AppSettings settings)
        {
            string GetColValue(int mappedIndex, string fallbackValue)
            {
                if (mappedIndex < 0 || mappedIndex >= cols.Length) return fallbackValue;
                string val = cols[mappedIndex].Trim();
                return string.IsNullOrWhiteSpace(val) ? fallbackValue : val;
            }

            string FormatName(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) return string.Empty;
                if (!settings.AutoCapitalizeNames) return name;
                var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
                return textInfo.ToTitleCase(name.ToLower());
            }

            return new Centriku.Models.Student
            {
                StudentID = GetColValue(settings.StudentIdColumnIndex, string.Empty),
                LastName = FormatName(GetColValue(settings.LastNameColumnIndex, string.Empty)),
                FirstName = FormatName(GetColValue(settings.FirstNameColumnIndex, string.Empty)),
                MiddleName = FormatName(GetColValue(settings.MiddleNameColumnIndex, string.Empty)),
                Suffix = FormatName(GetColValue(settings.SuffixColumnIndex, string.Empty)),
                Gender = GetColValue(settings.GenderColumnIndex, settings.DefaultGender),
                GradeYearLevel = GetColValue(settings.GradeLevelColumnIndex, settings.DefaultGradeLevel),
                Program = GetColValue(settings.ProgramColumnIndex, settings.DefaultProgram),
                SectionName = GetColValue(settings.SectionNameColumnIndex, settings.DefaultSectionName),
                EnrollmentStatus = GetColValue(settings.EnrollmentStatusColumnIndex, settings.DefaultEnrollmentStatus),
                IsArchived = false
            };
        }

        partial void OnSearchQueryChanged(string value) => UpdateDisplayedStudents();
        partial void OnArchiveSearchQueryChanged(string value) => UpdateDisplayedArchivedStudents();

        partial void OnSelectedTabIndexChanged(int value)
        {
            if (value == 1) _ = LoadImportSettingsPreviewAsync();
        }

        private void UpdateDisplayedArchivedStudents()
        {
            var lowerQuery = ArchiveSearchQuery?.ToLower() ?? string.Empty;

            var filtered = _allArchivedStudents.Where(s =>
                s.IsSelected || // STICKY SELECTION: Always keep checked students visible!
                string.IsNullOrWhiteSpace(lowerQuery) ||
                (s.StudentID?.ToLower().Contains(lowerQuery) == true) ||
                (s.LastName?.ToLower().Contains(lowerQuery) == true) ||
                (s.FirstName?.ToLower().Contains(lowerQuery) == true) ||
                (s.Program?.ToLower().Contains(lowerQuery) == true) ||
                (s.SectionName?.ToLower().Contains(lowerQuery) == true)
            ).ToList();

            DisplayedArchivedStudents = new ObservableCollection<StudentRowViewModel>(filtered);
            DynamicArchiveCounter = $"Showing: {filtered.Count} students";
        }

        private void UpdateDisplayedStudents()
        {
            var lowerQuery = SearchQuery?.ToLower() ?? string.Empty;

            bool MatchesFilters(StudentRowViewModel s)
            {
                if (!string.IsNullOrWhiteSpace(lowerQuery))
                {
                    bool matchesSearch =
                        (s.StudentID?.ToLower().Contains(lowerQuery) == true) ||
                        (s.LastName?.ToLower().Contains(lowerQuery) == true) ||
                        (s.FirstName?.ToLower().Contains(lowerQuery) == true) ||
                        (s.Program?.ToLower().Contains(lowerQuery) == true) ||
                        (s.SectionName?.ToLower().Contains(lowerQuery) == true);
                    if (!matchesSearch) return false;
                }

                if (SelectedYearFilter != "All Years" && s.GradeYearLevel != SelectedYearFilter) return false;
                if (SelectedProgramFilter != "All Programs" && s.Program != SelectedProgramFilter) return false;
                if (SelectedSectionFilter != "All Sections" && s.SectionName != SelectedSectionFilter) return false;
                if (SelectedStatusFilter != "All Statuses" && s.EnrollmentStatus != SelectedStatusFilter) return false;

                return true;
            }

            // STICKY SELECTION: Include the student if they are already checked OR if they match current filters
            var finalResults = _allStudents.Where(s => s.IsSelected || MatchesFilters(s)).ToList();
            DisplayedStudents = new ObservableCollection<StudentRowViewModel>(finalResults);
            DynamicStudentCounter = $"Showing: {finalResults.Count} students";
        }
    }
}