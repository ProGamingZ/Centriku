using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
    public partial class DirectoryViewModel 
    {
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

        partial void OnSearchQueryChanged(string value) => UpdateDisplayedStudents();
        partial void OnSelectedYearFilterChanged(string value) => UpdateDisplayedStudents();
        partial void OnSelectedProgramFilterChanged(string value) => UpdateDisplayedStudents();
        partial void OnSelectedSectionFilterChanged(string value) => UpdateDisplayedStudents();
        partial void OnSelectedStatusFilterChanged(string value) => UpdateDisplayedStudents();

        partial void OnSelectedTabIndexChanged(int value)
        {
            if (value == 1) _ = LoadImportSettingsPreviewAsync();
        }

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
            
            if (!row.IsEditing) 
            {
                row.OriginalStudentID = row.StudentID;
                row.IsEditing = true;
                return;
            }

            string oldId = (row.OriginalStudentID ?? string.Empty).Trim();
            string newId = (row.StudentID ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(newId) || string.IsNullOrWhiteSpace(row.LastName))
            {
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

            if (string.Equals(oldId, newId, StringComparison.Ordinal))
            {
                await db.UpdateAsync(row.DbModel);
            }
            else
            {
                var existingConflict = await db.Table<Centriku.Models.Student>()
                    .Where(s => s.StudentID == newId)
                    .FirstOrDefaultAsync();

                if (existingConflict != null && !string.Equals(oldId, newId, StringComparison.OrdinalIgnoreCase))
                {
                    row.StudentID = oldId;
                    row.IsEditing = false;
                    await LoadStudentsAsync();
                    return;
                }

                await db.InsertOrReplaceAsync(row.DbModel);

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
                catch { }

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

            var finalResults = _allStudents.Where(s => s.IsSelected || MatchesFilters(s)).ToList();
            DisplayedStudents = new ObservableCollection<StudentRowViewModel>(finalResults);
            DynamicStudentCounter = $"Showing: {finalResults.Count} students";
        }
    }
}