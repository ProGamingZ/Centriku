using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;

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

        public async void LoadStudents()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
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

        private async void SaveStudent()
        {
            if (string.IsNullOrWhiteSpace(NewStudentId) || string.IsNullOrWhiteSpace(NewStudentLastName)) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var newStudent = new Centriku.Models.Student
            {
                StudentID = NewStudentId, FirstName = NewStudentFirstName, MiddleName = NewStudentMiddleName, LastName = NewStudentLastName,
                Suffix = NewStudentSuffix, Gender = NewStudentGender, GradeYearLevel = NewStudentGradeYearLevel,
                Program = NewStudentProgram, SectionName = NewStudentSectionName, EnrollmentStatus = NewStudentEnrollmentStatus, IsArchived = false
            };
            await db.InsertOrReplaceAsync(newStudent);
            
            NewStudentId = string.Empty; NewStudentFirstName = string.Empty; NewStudentMiddleName = string.Empty;
            NewStudentLastName = string.Empty; NewStudentSuffix = string.Empty; NewStudentGradeYearLevel = string.Empty;
            NewStudentProgram = string.Empty; NewStudentSectionName = string.Empty; NewStudentEnrollmentStatus = "Regular";
            
            IsAddingStudent = false;
            LoadStudents();
        }

        private async void EditOrSaveStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            
            if (!row.IsEditing) 
            {
                row.IsEditing = true;
            }
            else 
            { 
                var db = new Centriku.Services.DatabaseService().GetConnection(); 
                // 1. Await the permanent database update
                await db.UpdateAsync(row.DbModel); 
                row.IsEditing = false; 
                // 2. Force the view to refresh and lock in the saved data!
                LoadStudents(); 
            }
        }

        private async void ArchiveStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            row.DbModel.IsArchived = true; await db.UpdateAsync(row.DbModel); LoadStudents(); OnStudentRosterChanged?.Invoke();
        }

        private async void RestoreStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            row.DbModel.IsArchived = false; await db.UpdateAsync(row.DbModel); LoadStudents(); OnStudentRosterChanged?.Invoke();
        }

        private async void DeleteStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.DeleteAsync(row.DbModel); LoadStudents(); 
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
                var db = new Centriku.Services.DatabaseService().GetConnection();
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
                            bool isDup = existingIds.Contains(parsed.StudentID);
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
            var db = new Centriku.Services.DatabaseService().GetConnection();
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
                    bool isDup = existingIds.Contains(parsed.StudentID);
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
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();
            var toInsert = new List<Centriku.Models.Student>(); var toUpdate = new List<Centriku.Models.Student>();

            foreach(var staged in StagedStudents)
            {
                if (staged.IsError) continue; 
                if (staged.IsDuplicate) { if (settings.DuplicateHandlingRule == "Update") toUpdate.Add(staged.DbModel); }
                else toInsert.Add(staged.DbModel);
            }
            if (toInsert.Any()) await db.InsertAllAsync(toInsert, runInTransaction: true);
            if (toUpdate.Any()) await db.UpdateAllAsync(toUpdate, runInTransaction: true);
            CancelBulkImport(); LoadStudents();     
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

        private void UpdateDisplayedArchivedStudents()
        {
            if (string.IsNullOrWhiteSpace(ArchiveSearchQuery)) { DisplayedArchivedStudents = new ObservableCollection<StudentRowViewModel>(_allArchivedStudents); return; }
            var lowerQuery = ArchiveSearchQuery.ToLower();
            var filtered = _allArchivedStudents.Where(s => (s.StudentID?.Contains(lowerQuery) == true) || (s.LastName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || (s.FirstName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || (s.Program?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || (s.SectionName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true));
            DisplayedArchivedStudents = new ObservableCollection<StudentRowViewModel>(filtered);
        }

        private void UpdateDisplayedStudents()
        {
            var filtered = _allStudents.AsEnumerable();

            // 1. Text Search Filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var lowerQuery = SearchQuery.ToLower();
                filtered = filtered.Where(s => 
                    (s.StudentID?.ToLower().Contains(lowerQuery) == true) || 
                    (s.LastName?.ToLower().Contains(lowerQuery) == true) || 
                    (s.FirstName?.ToLower().Contains(lowerQuery) == true) || 
                    (s.Program?.ToLower().Contains(lowerQuery) == true) || 
                    (s.SectionName?.ToLower().Contains(lowerQuery) == true));
            }

            // 2. Dropdown Filters
            if (SelectedYearFilter != "All Years") filtered = filtered.Where(s => s.GradeYearLevel == SelectedYearFilter);
            if (SelectedProgramFilter != "All Programs") filtered = filtered.Where(s => s.Program == SelectedProgramFilter);
            if (SelectedSectionFilter != "All Sections") filtered = filtered.Where(s => s.SectionName == SelectedSectionFilter);
            if (SelectedStatusFilter != "All Statuses") filtered = filtered.Where(s => s.EnrollmentStatus == SelectedStatusFilter);

            var finalResults = filtered.ToList();
            DisplayedStudents = new ObservableCollection<StudentRowViewModel>(finalResults);
            
            // 3. Update the Dynamic Counter
            DynamicStudentCounter = $"Showing: {finalResults.Count} students";
        }
    }
}