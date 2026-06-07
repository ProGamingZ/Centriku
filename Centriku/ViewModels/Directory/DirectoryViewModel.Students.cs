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
   // Handles everything related to managing the master school roster, bulk imports, filtering, and archiving
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
        [ObservableProperty] public partial ObservableCollection<StudentRowViewModel> DisplayedArchivedStudents { get; set; } = new();
        [ObservableProperty] public partial string ArchiveSearchQuery { get; set; } = string.Empty;

        // Archive Column Visibility
        [ObservableProperty] public partial bool ShowArchiveLrnColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveLastNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveFirstNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveMiddleNameColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveSuffixColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveGenderColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveGradeYearLevelColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveSectionBlockColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveEnrollmentStatusColumn { get; set; } = true;

        // Master Column Visibility
        [ObservableProperty] public partial bool ShowLrnColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowLastNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowFirstNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowMiddleNameColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowSuffixColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowGenderColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowGradeYearLevelColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowSectionBlockColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowEnrollmentStatusColumn { get; set; } = true;

        // Add Student Form Properties
        [ObservableProperty] public partial bool IsAddingStudent { get; set; } = false;
        [ObservableProperty] public partial string NewStudentLrn { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentFirstName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentMiddleName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentLastName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentSuffix { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentGender { get; set; } = "Male";
        [ObservableProperty] public partial string NewStudentGradeYearLevel { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentSectionBlock { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentEnrollmentStatus { get; set; } = "Regular";

        private async void LoadStudents()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var rawStudents = await db.Table<Centriku.Models.Student>().ToListAsync();
            
            _allStudents = rawStudents.Where(s => !s.IsArchived).Select(s => new StudentRowViewModel(s)).ToList();
            _allArchivedStudents = rawStudents.Where(s => s.IsArchived).Select(s => new StudentRowViewModel(s)).ToList();

            UpdateDisplayedStudents();
            UpdateDisplayedArchivedStudents();
        }

        private async void SaveStudent()
        {
            if (string.IsNullOrWhiteSpace(NewStudentLrn) || string.IsNullOrWhiteSpace(NewStudentLastName)) return;

            var db = new Centriku.Services.DatabaseService().GetConnection();
            var newStudent = new Centriku.Models.Student
            {
                StudentID = NewStudentLrn, FirstName = NewStudentFirstName, MiddleName = NewStudentMiddleName, LastName = NewStudentLastName,
                Suffix = NewStudentSuffix, Gender = NewStudentGender, GradeYearLevel = NewStudentGradeYearLevel,
                SectionBlock = NewStudentSectionBlock, EnrollmentStatus = NewStudentEnrollmentStatus, IsArchived = false
            };

            await db.InsertOrReplaceAsync(newStudent);
            
            NewStudentLrn = string.Empty; NewStudentFirstName = string.Empty; NewStudentMiddleName = string.Empty;
            NewStudentLastName = string.Empty; NewStudentSuffix = string.Empty; NewStudentGradeYearLevel = string.Empty;
            NewStudentSectionBlock = string.Empty; NewStudentEnrollmentStatus = "Regular";
            
            IsAddingStudent = false;
            LoadStudents();
        }

        private async void EditOrSaveStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            if (!row.IsEditing) row.IsEditing = true;
            else
            {
                var db = new Centriku.Services.DatabaseService().GetConnection();
                await db.UpdateAsync(row.DbModel);
                row.IsEditing = false;
            }
        }

        private async void ArchiveStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            row.DbModel.IsArchived = true;
            await db.UpdateAsync(row.DbModel);
            LoadStudents(); 
            OnStudentRosterChanged?.Invoke();
        }

        private async void RestoreStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            row.DbModel.IsArchived = false;
            await db.UpdateAsync(row.DbModel);
            LoadStudents(); 
            OnStudentRosterChanged?.Invoke();
        }

        private async void DeleteStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.DeleteAsync(row.DbModel);
            LoadStudents(); 
        }

        [ObservableProperty] public partial ObservableCollection<StagedStudent> StagedStudents { get; set; } = new();
        [ObservableProperty] public partial bool HasStagedStudents { get; set; } = false;
        [ObservableProperty] public partial bool HasImportError { get; set; } = false;
        [ObservableProperty] public partial string ImportSummaryMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsLoading { get; set; } = false;
        [RelayCommand]
        public static void NavigateToSettings()
        {
            OnNavigateToSettingsBulkImportTab?.Invoke();
        }
        public static event System.Action? OnNavigateToSettingsBulkImportTab;
        public async Task ProcessBulkImportAsync(string filePath)
        {
            try
            {
                StagedStudents.Clear();
                HasImportError = false;
                HasStagedStudents = false;
                IsLoading = true; 
                ImportSummaryMessage = string.Empty;

                var db = new Centriku.Services.DatabaseService().GetConnection();
                var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();
                
                // Fetch existing LRNs first so we can check for duplicates safely
                var existingStudents = await db.Table<Centriku.Models.Student>().ToListAsync();
                var existingLrns = existingStudents.Select(s => s.StudentID).ToHashSet();

                // Run the heavy Excel parsing in a background thread so the UI doesn't freeze!
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

                        bool isError = false;
                        string errorMsg = "";

                        // Rule 1: It is an error if there is absolutely no LRN
                        if (string.IsNullOrWhiteSpace(parsed.StudentID)) { isError = true; errorMsg = "Error: Missing LRN"; }
                        
                        // Rule 2: Check Ghost Row rules from Settings
                        else if (settings.SkipIncompleteRows)
                        {
                            if (settings.LastNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.LastName)) { isError = true; errorMsg = "Error: Missing Last Name"; }
                            else if (settings.FirstNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.FirstName)) { isError = true; errorMsg = "Error: Missing First Name"; }
                        }

                        // Apply the Badges!
                        if (isError)
                        {
                            staged.IsError = true;
                            staged.ImportStatus = errorMsg;
                            staged.StatusColor = "#EF4444"; // Error Red
                        }
                        else
                        {
                            bool isDup = existingLrns.Contains(parsed.StudentID);
                            if (isDup)
                            {
                                staged.IsDuplicate = true;
                                if (settings.DuplicateHandlingRule == "Skip") { staged.ImportStatus = "Will Skip"; staged.StatusColor = "#6B7280"; } 
                                else { staged.ImportStatus = "Will Update"; staged.StatusColor = "#EAB308"; } 
                            }
                            else
                            {
                                staged.ImportStatus = "New Student";
                                staged.StatusColor = "#22C55E"; // Success Green
                            }
                        }
                        tempList.Add(staged);
                    }

                    // Parse CSV
                    if (extension == ".csv")
                    {
                        var lines = File.ReadAllLines(filePath);
                        int startRow = settings.SkipFirstRow ? 1 : 0; 
                        for (int i = startRow; i < lines.Length; i++) ProcessRow(lines[i].Split(','));
                    }
                    // Parse Excel
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

                if (!stagedList.Any())
                {
                    HasImportError = true;
                    ImportSummaryMessage = "Error: The file appears to be empty or unreadable.";
                }
                else
                {
                    StagedStudents = new ObservableCollection<StagedStudent>(stagedList);
                    HasStagedStudents = true;
                    
                    int errors = stagedList.Count(s => s.IsError);
                    string dupMsg = stagedList.Any(s => s.IsDuplicate) ? $" ({stagedList.Count(s => s.IsDuplicate)} duplicates)" : "";
                    
                    // Dynamic messaging based on whether errors were found!
                    if (errors > 0) ImportSummaryMessage = $"Warning: Found {stagedList.Count} rows, but {errors} have missing information. Please fix the red rows below.";
                    else ImportSummaryMessage = $"Success! Read {stagedList.Count} valid students{dupMsg}. Please review the table below.";
                }
            }
            catch (Exception ex) { HasImportError = true; ImportSummaryMessage = $"File Error: {ex.Message}"; }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        public async Task RecheckStagedDataAsync()
        {
            if (!StagedStudents.Any()) return;

            IsLoading = true;
            ImportSummaryMessage = "Re-evaluating data...";

            var db = new Centriku.Services.DatabaseService().GetConnection();
            var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();
            
            var existingStudents = await db.Table<Centriku.Models.Student>().ToListAsync();
            var existingLrns = existingStudents.Select(s => s.StudentID).ToHashSet();

            int errorCount = 0;

            // Re-scan every student in the table to see if the user fixed them!
            foreach (var staged in StagedStudents)
            {
                var parsed = staged.DbModel;
                bool isError = false;
                string errorMsg = "";

                if (string.IsNullOrWhiteSpace(parsed.StudentID)) { isError = true; errorMsg = "Error: Missing LRN"; }
                else if (settings.SkipIncompleteRows)
                {
                    if (settings.LastNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.LastName)) { isError = true; errorMsg = "Error: Missing Last Name"; }
                    else if (settings.FirstNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.FirstName)) { isError = true; errorMsg = "Error: Missing First Name"; }
                }

                if (isError)
                {
                    staged.IsError = true;
                    staged.ImportStatus = errorMsg;
                    staged.StatusColor = "#EF4444"; // Still broken (Red)
                    errorCount++;
                }
                else
                {
                    staged.IsError = false; // Fixed!
                    bool isDup = existingLrns.Contains(parsed.StudentID);
                    if (isDup)
                    {
                        staged.IsDuplicate = true;
                        if (settings.DuplicateHandlingRule == "Skip") { staged.ImportStatus = "Will Skip"; staged.StatusColor = "#6B7280"; } 
                        else { staged.ImportStatus = "Will Update"; staged.StatusColor = "#EAB308"; } 
                    }
                    else
                    {
                        staged.IsDuplicate = false;
                        staged.ImportStatus = "New Student";
                        staged.StatusColor = "#22C55E"; // Success Green!
                    }
                }
            }

            int total = StagedStudents.Count;
            int dupes = StagedStudents.Count(s => s.IsDuplicate);
            string dupMsg = dupes > 0 ? $" ({dupes} duplicates)" : "";

            if (errorCount > 0) ImportSummaryMessage = $"Warning: Found {total} rows, but {errorCount} still have missing information. Please fix the red rows.";
            else ImportSummaryMessage = $"Success! All {total} students are valid{dupMsg}. Ready to save.";

            IsLoading = false;
        }

        [RelayCommand]
        public async Task ConfirmBulkImportAsync()
        {
            if (!StagedStudents.Any()) return;
            
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();

            var toInsert = new List<Centriku.Models.Student>();
            var toUpdate = new List<Centriku.Models.Student>();

            foreach(var staged in StagedStudents)
            {
                if (staged.IsError) continue; 

                if (staged.IsDuplicate)
                {
                    if (settings.DuplicateHandlingRule == "Update") toUpdate.Add(staged.DbModel);
                }
                else
                {
                    toInsert.Add(staged.DbModel);
                }
            }

            if (toInsert.Any()) await db.InsertAllAsync(toInsert, runInTransaction: true);
            if (toUpdate.Any()) await db.UpdateAllAsync(toUpdate, runInTransaction: true);

            CancelBulkImport(); 
            LoadStudents();     
        }
        
        [RelayCommand]
        public void CancelBulkImport()
        {
            StagedStudents.Clear();
            HasStagedStudents = false;
            HasImportError = false;
            ImportSummaryMessage = string.Empty;
        }
        
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
                
                // If the user turned it off, just return the raw text
                if (!settings.AutoCapitalizeNames) return name;
                
                // C# requires converting to lowercase first to fix "ALL CAPS" typing, then to Title Case!
                var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
                return textInfo.ToTitleCase(name.ToLower());
            }

            return new Centriku.Models.Student
            {
                StudentID = GetColValue(settings.LrnColumnIndex, string.Empty),
                LastName = FormatName(GetColValue(settings.LastNameColumnIndex, string.Empty)),
                FirstName = FormatName(GetColValue(settings.FirstNameColumnIndex, string.Empty)),
                MiddleName = FormatName(GetColValue(settings.MiddleNameColumnIndex, string.Empty)),
                Suffix = FormatName(GetColValue(settings.SuffixColumnIndex, string.Empty)),
                
                Gender = GetColValue(settings.GenderColumnIndex, settings.DefaultGender),
                GradeYearLevel = GetColValue(settings.GradeLevelColumnIndex, settings.DefaultGradeLevel),
                SectionBlock = GetColValue(settings.SectionColumnIndex, settings.DefaultSection),
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
            var filtered = _allArchivedStudents.Where(s => (s.StudentID?.Contains(lowerQuery) == true) || (s.LastName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || (s.FirstName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || (s.GradeYearLevel?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || (s.SectionBlock?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true));
            DisplayedArchivedStudents = new ObservableCollection<StudentRowViewModel>(filtered);
        }

        private void UpdateDisplayedStudents()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) { DisplayedStudents = new ObservableCollection<StudentRowViewModel>(_allStudents); return; }
            var lowerQuery = SearchQuery.ToLower();
            var filtered = _allStudents.Where(s => (s.StudentID?.Contains(lowerQuery) == true) || (s.LastName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || (s.FirstName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || (s.GradeYearLevel?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || (s.SectionBlock?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true));
            DisplayedStudents = new ObservableCollection<StudentRowViewModel>(filtered);
        }
    }
}