using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
            [ObservableProperty] public partial string StatusColor { get; set; } = "#2210B981";
            [ObservableProperty] public partial string StatusBorderColor { get; set; } = "#10B981";
            [ObservableProperty] public partial string StatusTextColor { get; set; } = "#10B981";
            public StagedStudent(Centriku.Models.Student student) { DbModel = student; }
        }

        [ObservableProperty] public partial ObservableCollection<Centriku.ViewModels.Settings.PreviewStudentRow> CurrentSettingsPreview { get; set; } = new();
        [ObservableProperty] public partial string AutoFillSummaryText { get; set; } = string.Empty;
        [ObservableProperty] public partial string Col1H { get; set; } = ""; [ObservableProperty] public partial string Col2H { get; set; } = "";
        [ObservableProperty] public partial string Col3H { get; set; } = ""; [ObservableProperty] public partial string Col4H { get; set; } = "";
        [ObservableProperty] public partial string Col5H { get; set; } = ""; [ObservableProperty] public partial string Col6H { get; set; } = "";
        [ObservableProperty] public partial string Col7H { get; set; } = ""; [ObservableProperty] public partial string Col8H { get; set; } = "";
        [ObservableProperty] public partial string Col9H { get; set; } = ""; [ObservableProperty] public partial string Col10H { get; set; } = "";

        [ObservableProperty] public partial ObservableCollection<StagedStudent> StagedStudents { get; set; } = new();
        [ObservableProperty] public partial bool HasStagedStudents { get; set; } = false;
        [ObservableProperty] public partial bool HasImportError { get; set; } = false;
        [ObservableProperty] public partial string ImportSummaryMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsLoading { get; set; } = false;
        
        [RelayCommand] public static void NavigateToSettings() => OnNavigateToSettingsBulkImportTab?.Invoke();
        public static event Action? OnNavigateToSettingsBulkImportTab;

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
        
            string genderDef = string.IsNullOrWhiteSpace(settings.DefaultGender) || settings.DefaultGender == "None" ? "None" : settings.DefaultGender;
            string yearDef = string.IsNullOrWhiteSpace(settings.DefaultGradeLevel) ? "None" : settings.DefaultGradeLevel;
            string progDef = string.IsNullOrWhiteSpace(settings.DefaultProgram) ? "None" : settings.DefaultProgram;
            string secDef = string.IsNullOrWhiteSpace(settings.DefaultSectionName) ? "None" : settings.DefaultSectionName;
            string statDef = string.IsNullOrWhiteSpace(settings.DefaultEnrollmentStatus) ? "Regular" : settings.DefaultEnrollmentStatus;
            string dupRule = settings.DuplicateHandlingRule == "Skip" ? "Skip Duplicates" : "Update Existing";

            AutoFillSummaryText = $"Gender: {genderDef}   •   Year: {yearDef}   •   Program: {progDef}   •   Section: {secDef}   •   Status: {statDef}   •   Rule: {dupRule}";
        }

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

                        if (isError) { staged.IsError = true; ApplyStagedBadgeTheme(staged, errorMsg, "Error"); }
                        else
                        {
                            bool isDup = false;
                            
                            if (!string.IsNullOrWhiteSpace(parsed.StudentID) && existingIds.Contains(parsed.StudentID)) 
                            {
                                isDup = true;
                            } 
                            else 
                            {
                                var nameMatch = existingStudents.FirstOrDefault(e => 
                                    string.Equals(e.LastName, parsed.LastName, StringComparison.OrdinalIgnoreCase) && 
                                    string.Equals(e.FirstName, parsed.FirstName, StringComparison.OrdinalIgnoreCase));
                                
                                if (nameMatch != null) 
                                {
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
                                if (settings.DuplicateHandlingRule == "Skip") { ApplyStagedBadgeTheme(staged, "Will Skip", "Skip"); } 
                                else { ApplyStagedBadgeTheme(staged, "Will Update", "Update"); } 
                            }
                            else { ApplyStagedBadgeTheme(staged, "New Student", "New"); }
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

        private static void ApplyStagedBadgeTheme(StagedStudent staged, string status, string category)
        {
            staged.ImportStatus = status;
            switch (category)
            {
                case "Error":
                    staged.StatusColor = "#22F43F5E";
                    staged.StatusBorderColor = "#F43F5E";
                    staged.StatusTextColor = "#F43F5E";
                    break;
                case "Skip":
                    staged.StatusColor = "#26334D";
                    staged.StatusBorderColor = "#94A3B8";
                    staged.StatusTextColor = "#94A3B8";
                    break;
                case "Update":
                    staged.StatusColor = "#22F59E0B";
                    staged.StatusBorderColor = "#F59E0B";
                    staged.StatusTextColor = "#F59E0B";
                    break;
                default:
                    staged.StatusColor = "#2210B981";
                    staged.StatusBorderColor = "#10B981";
                    staged.StatusTextColor = "#10B981";
                    break;
            }
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

                if (isError) { staged.IsError = true; ApplyStagedBadgeTheme(staged, errorMsg, "Error"); errorCount++; }
                else
                {
                    staged.IsError = false; 
                    bool isDup = false;
                    
                    if (!string.IsNullOrWhiteSpace(parsed.StudentID) && existingIds.Contains(parsed.StudentID)) 
                    {
                        isDup = true;
                    } 
                    else 
                    {
                        var nameMatch = existingStudents.FirstOrDefault(e => 
                            string.Equals(e.LastName, parsed.LastName, StringComparison.OrdinalIgnoreCase) && 
                            string.Equals(e.FirstName, parsed.FirstName, StringComparison.OrdinalIgnoreCase));
                        
                        if (nameMatch != null) 
                        {
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
                        if (settings.DuplicateHandlingRule == "Skip") { ApplyStagedBadgeTheme(staged, "Will Skip", "Skip"); } 
                        else { ApplyStagedBadgeTheme(staged, "Will Update", "Update"); } 
                    }
                    else { staged.IsDuplicate = false; ApplyStagedBadgeTheme(staged, "New Student", "New"); }
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

            await RecheckStagedDataAsync();

            var db = _dbService.GetConnection();
            var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();
            
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
    }
}