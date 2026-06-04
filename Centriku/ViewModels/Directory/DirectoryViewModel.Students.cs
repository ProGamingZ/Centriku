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
        public class StagedStudent
        {
            public Centriku.Models.Student DbModel { get; set; } = new();
            public bool IsDuplicate { get; set; }
            public string ImportStatus { get; set; } = string.Empty;
            public string StatusColor { get; set; } = "#FFFFFF";
            
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
        }

        private async void RestoreStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            row.DbModel.IsArchived = false;
            await db.UpdateAsync(row.DbModel);
            LoadStudents(); 
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
                ImportSummaryMessage = "Reading file...";

                var db = new Centriku.Services.DatabaseService().GetConnection();
                var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();

                var newStudents = new List<Centriku.Models.Student>();
                string extension = Path.GetExtension(filePath).ToLower();
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                int ghostCount = 0;

                if (extension == ".csv")
                {
                    var lines = await File.ReadAllLinesAsync(filePath);
                    int startRow = settings.SkipFirstRow ? 1 : 0; 
                    for (int i = startRow; i < lines.Length; i++) 
                    {
                        var cols = lines[i].Split(',');
                        if (cols.Length == 0) continue; 
                        
                        var parsed = ParseStudentRow(cols, settings);

                        bool isGhost = false;
                        if (settings.SkipIncompleteRows)
                        {
                            if (settings.LrnColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.StudentID)) isGhost = true;
                            if (settings.LastNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.LastName)) isGhost = true;
                            if (settings.FirstNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.FirstName)) isGhost = true;
                            // Note: Removed MiddleName ghost check because some students don't have middle names!
                        }

                        if (string.IsNullOrWhiteSpace(parsed.StudentID)) isGhost = true;

                        if (isGhost) { ghostCount++; continue; }
                        newStudents.Add(parsed);
                    }
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
                        
                        if (cols.Length == 0) continue;
                        
                        var parsed = ParseStudentRow(cols, settings);

                        bool isGhost = false;
                        if (settings.SkipIncompleteRows)
                        {
                            if (settings.LrnColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.StudentID)) isGhost = true;
                            if (settings.LastNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.LastName)) isGhost = true;
                            if (settings.FirstNameColumnIndex != -1 && string.IsNullOrWhiteSpace(parsed.FirstName)) isGhost = true;
                        }

                        if (string.IsNullOrWhiteSpace(parsed.StudentID)) isGhost = true;

                        if (isGhost) { ghostCount++; continue; }
                        newStudents.Add(parsed);
                    }
                }

                // If 100% of rows were ghosts or empty, trigger Error State!
                if (!newStudents.Any())
                {
                    HasImportError = true;
                    ImportSummaryMessage = $"Error: No valid students found in the file. {ghostCount} rows were skipped because they were missing LRNs or Names. Please check your File Mapping in Settings.";
                    return;
                }

                // The Duplicate Checker!
                var existingStudents = await db.Table<Centriku.Models.Student>().ToListAsync();
                var existingLrns = existingStudents.Select(s => s.StudentID).ToHashSet();

                var stagedList = new List<StagedStudent>();

                foreach (var student in newStudents)
                {
                    bool isDup = existingLrns.Contains(student.StudentID);
                    string status = "New Student";
                    string color = "#22C55E"; // Success Green

                    if (isDup)
                    {
                        if (settings.DuplicateHandlingRule == "Skip") { status = "Will Skip"; color = "#6B7280"; } // Ignore Gray
                        else { status = "Will Update"; color = "#EAB308"; } // Warning Yellow
                    }

                    stagedList.Add(new StagedStudent(student) { IsDuplicate = isDup, ImportStatus = status, StatusColor = color });
                }

                StagedStudents = new ObservableCollection<StagedStudent>(stagedList);
                HasStagedStudents = true;
                
                string dupMsg = existingLrns.Any() ? $" ({stagedList.Count(s => s.IsDuplicate)} are duplicates)" : "";
                ImportSummaryMessage = $"Success! Read {stagedList.Count} valid students{dupMsg}. {ghostCount} incomplete ghost rows were ignored. Please review the table below.";
            }
            catch (Exception ex) 
            { 
                HasImportError = true;
                ImportSummaryMessage = $"File Error: The file might be corrupted or open in another program. Details: {ex.Message}";
            }
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

            CancelBulkImport(); // Clear the waiting room
            LoadStudents();     // Refresh the main directory
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