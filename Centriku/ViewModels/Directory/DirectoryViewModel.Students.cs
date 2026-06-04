using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
   // Handles everything related to managing the master school roster, bulk imports, filtering, and archiving
    public partial class DirectoryViewModel 
    {
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

        public async Task ProcessBulkImportAsync(string filePath)
        {
            try
            {
                var db = new Centriku.Services.DatabaseService().GetConnection();
                var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();

                var newStudents = new List<Centriku.Models.Student>();
                string extension = Path.GetExtension(filePath).ToLower();
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                if (extension == ".csv")
                {
                    var lines = await File.ReadAllLinesAsync(filePath);
                    int startRow = settings.SkipFirstRow ? 1 : 0; 
                    for (int i = startRow; i < lines.Length; i++) 
                    {
                        var cols = lines[i].Split(',');
                        if (cols.Length == 0 || settings.LrnColumnIndex == -1) continue; 
                        newStudents.Add(ParseStudentRow(cols, settings));
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
                        if (cols.Length == 0 || settings.LrnColumnIndex == -1 || string.IsNullOrWhiteSpace(cols[settings.LrnColumnIndex])) continue;
                        newStudents.Add(ParseStudentRow(cols, settings));
                    }
                }

                if (newStudents.Any())
                {
                    // PHASE 1: THE DUPLICATE STUDENT ENGINE
                    var existingStudents = await db.Table<Centriku.Models.Student>().ToListAsync();
                    var existingLrns = existingStudents.Select(s => s.StudentID).ToHashSet();

                    var studentsToInsert = new List<Centriku.Models.Student>();
                    var studentsToUpdate = new List<Centriku.Models.Student>();

                    foreach (var student in newStudents)
                    {
                        if (existingLrns.Contains(student.StudentID))
                        {
                            // It's a duplicate! Obey the user's settings.
                            if (settings.DuplicateHandlingRule == "Update")
                            {
                                studentsToUpdate.Add(student);
                            }
                            // If it's set to "Skip", it does absolutely nothing and gets ignored!
                        }
                        else
                        {
                            // Brand new student!
                            studentsToInsert.Add(student);
                        }
                    }

                    // Execute database commands based on our separated buckets
                    if (studentsToInsert.Any()) await db.InsertAllAsync(studentsToInsert, runInTransaction: true);
                    if (studentsToUpdate.Any()) await db.UpdateAllAsync(studentsToUpdate, runInTransaction: true);
                    
                    LoadStudents(); 
                }
            }
            catch (Exception ex) { Console.WriteLine($"Bulk Import failed: {ex.Message}"); }
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