using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using ExcelDataReader;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels
{
    public partial class DirectoryViewModel : ViewModelBase
    {
        [ObservableProperty] public partial ObservableCollection<StudentClassPerformanceViewModel> SelectedStudentClasses { get; set; } = new();
        [ObservableProperty] public partial bool HasEnrolledClasses { get; set; } = false;
        
        private List<StudentRowViewModel> _allStudents = [];
        private List<StudentRowViewModel> _allArchivedStudents = [];
        [ObservableProperty] public partial ObservableCollection<StudentRowViewModel> DisplayedStudents { get; set; } = new();
        [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;
        [ObservableProperty] public partial ObservableCollection<StudentRowViewModel> DisplayedArchivedStudents { get; set; } = new();
        [ObservableProperty] public partial string ArchiveSearchQuery { get; set; } = string.Empty;

        // Archive Column Visibility Toggles
        [ObservableProperty] public partial bool ShowArchiveLrnColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveLastNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveFirstNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveMiddleNameColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveSuffixColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveGenderColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveGradeYearLevelColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveSectionBlockColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveEnrollmentStatusColumn { get; set; } = true;

        // --- Master Column Visibility Toggles ---
        [ObservableProperty] public partial bool ShowLrnColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowLastNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowFirstNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowMiddleNameColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowSuffixColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowGenderColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowGradeYearLevelColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowSectionBlockColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowEnrollmentStatusColumn { get; set; } = true;

        // --- Add Student Form Properties ---
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

        [ObservableProperty] public partial bool IsProfileOpen { get; set; } = false;
        [ObservableProperty] public partial StudentRowViewModel? SelectedProfile { get; set; }
        [ObservableProperty] public partial StudentClassPerformanceViewModel? SelectedStudentClassPerformance { get; set; }

        public IRelayCommand ToggleAddStudentFormCommand { get; }
        public IRelayCommand SaveStudentCommand { get; }
        public IRelayCommand<StudentRowViewModel> EditOrSaveStudentCommand { get; }
        public IRelayCommand<StudentRowViewModel> ViewProfileCommand { get; }
        public IRelayCommand<StudentRowViewModel> ArchiveStudentCommand { get; }
        public IRelayCommand<StudentRowViewModel> RestoreStudentCommand { get; } 
        public IRelayCommand<StudentRowViewModel> DeleteStudentCommand { get; } 
        public IRelayCommand CloseProfileCommand { get; }

        public DirectoryViewModel()
        {
            ToggleAddStudentFormCommand = new RelayCommand(() => IsAddingStudent = !IsAddingStudent);
            SaveStudentCommand = new RelayCommand(SaveStudent);
            EditOrSaveStudentCommand = new RelayCommand<StudentRowViewModel>(EditOrSaveStudent!);
            ViewProfileCommand = new RelayCommand<StudentRowViewModel>(ViewProfile!);
            
            ArchiveStudentCommand = new RelayCommand<StudentRowViewModel>(ArchiveStudent!); 
            RestoreStudentCommand = new RelayCommand<StudentRowViewModel>(RestoreStudent!); 
            DeleteStudentCommand = new RelayCommand<StudentRowViewModel>(DeleteStudent!);
            CloseProfileCommand = new RelayCommand(() => IsProfileOpen = false); 

            LoadStudents();
        }

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
            if (string.IsNullOrWhiteSpace(NewStudentLrn) || string.IsNullOrWhiteSpace(NewStudentLastName))
                return;

            var db = new Centriku.Services.DatabaseService().GetConnection();
            var newStudent = new Centriku.Models.Student
            {
                StudentID = NewStudentLrn, 
                FirstName = NewStudentFirstName,
                MiddleName = NewStudentMiddleName,
                LastName = NewStudentLastName,
                Suffix = NewStudentSuffix,
                Gender = NewStudentGender,
                GradeYearLevel = NewStudentGradeYearLevel,
                SectionBlock = NewStudentSectionBlock,
                EnrollmentStatus = NewStudentEnrollmentStatus,
                IsArchived = false
            };

            await db.InsertOrReplaceAsync(newStudent);
            
            NewStudentLrn = string.Empty;
            NewStudentFirstName = string.Empty;
            NewStudentMiddleName = string.Empty;
            NewStudentLastName = string.Empty;
            NewStudentSuffix = string.Empty;
            NewStudentGradeYearLevel = string.Empty;
            NewStudentSectionBlock = string.Empty;
            NewStudentEnrollmentStatus = "Regular";
            
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
                var newStudents = new List<Centriku.Models.Student>();
                string extension = Path.GetExtension(filePath).ToLower();

                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                if (extension == ".csv")
                {
                    var lines = await File.ReadAllLinesAsync(filePath);
                    for (int i = 1; i < lines.Length; i++) 
                    {
                        var cols = lines[i].Split(',');
                        if (cols.Length < 3) continue;

                        newStudents.Add(ParseStudentRow(cols));
                    }
                }
                else if (extension == ".xlsx" || extension == ".xls")
                {
                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        reader.Read(); 

                        while (reader.Read())
                        {
                            var cols = new string[reader.FieldCount];
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                cols[i] = reader.GetValue(i)?.ToString() ?? string.Empty;
                            }

                            if (cols.Length < 3 || string.IsNullOrWhiteSpace(cols[0])) continue;

                            newStudents.Add(ParseStudentRow(cols));
                        }
                    }
                }
                else
                {
                    System.Console.WriteLine("Unsupported file format.");
                    return;
                }

                if (newStudents.Any())
                {
                    var db = new Centriku.Services.DatabaseService().GetConnection();
                    await db.InsertAllAsync(newStudents, runInTransaction: true);
                    
                    LoadStudents(); 
                    System.Console.WriteLine($"Successfully imported {newStudents.Count} students.");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Bulk Import failed: {ex.Message}");
            }
        }

        private static Centriku.Models.Student ParseStudentRow(string[] cols)
        {
            return new Centriku.Models.Student
            {
                StudentID = cols[0].Trim(),
                LastName = cols[1].Trim(),
                FirstName = cols[2].Trim(),
                MiddleName = cols.Length > 3 ? cols[3].Trim() : string.Empty,
                Suffix = cols.Length > 4 ? cols[4].Trim() : string.Empty,
                Gender = cols.Length > 5 ? cols[5].Trim() : "Male",
                GradeYearLevel = cols.Length > 6 ? cols[6].Trim() : string.Empty,
                SectionBlock = cols.Length > 7 ? cols[7].Trim() : string.Empty,
                EnrollmentStatus = cols.Length > 8 ? cols[8].Trim() : "Regular",
                IsArchived = false
            };
        }

        partial void OnSearchQueryChanged(string value)
        {
            UpdateDisplayedStudents();
        }

        partial void OnArchiveSearchQueryChanged(string value)
        {
            UpdateDisplayedArchivedStudents();
        }

        private void UpdateDisplayedArchivedStudents()
        {
            if (string.IsNullOrWhiteSpace(ArchiveSearchQuery))
            {
                DisplayedArchivedStudents = new ObservableCollection<StudentRowViewModel>(_allArchivedStudents);
                return;
            }
            var lowerQuery = ArchiveSearchQuery.ToLower();
            
            var filtered = _allArchivedStudents.Where(s => 
                (s.StudentID?.Contains(lowerQuery) == true) || 
                (s.LastName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || 
                (s.FirstName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (s.GradeYearLevel?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (s.SectionBlock?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true));

            DisplayedArchivedStudents = new ObservableCollection<StudentRowViewModel>(filtered);
        }

        private void UpdateDisplayedStudents()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                DisplayedStudents = new ObservableCollection<StudentRowViewModel>(_allStudents);
                return;
            }
            var lowerQuery = SearchQuery.ToLower();
            
            var filtered = _allStudents.Where(s => 
                (s.StudentID?.Contains(lowerQuery) == true) || 
                (s.LastName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || 
                (s.FirstName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (s.GradeYearLevel?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (s.SectionBlock?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true));

            DisplayedStudents = new ObservableCollection<StudentRowViewModel>(filtered);
        }

        private async void ViewProfile(StudentRowViewModel row)
        {
            if (row == null) return;
            SelectedProfile = row;
            IsProfileOpen = true;
            await LoadStudentPerformanceAsync(row.StudentID);
        }

        private async Task LoadStudentPerformanceAsync(string studentId)
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var performanceList = new List<StudentClassPerformanceViewModel>();

            var rosters = await db.Table<Centriku.Models.ClassRoster>().Where(r => r.StudentID == studentId).ToListAsync();
                                  
            foreach (var roster in rosters)
            {
                var tClass = await db.Table<Centriku.Models.TeacherClass>().Where(c => c.ClassID == roster.ClassID).FirstOrDefaultAsync();
                if (tClass == null) continue;

                var template = await db.Table<Centriku.Models.GradingTemplate>().Where(t => t.TemplateID == tClass.GradingTemplateID).FirstOrDefaultAsync();
                
                var attendance = await db.Table<Centriku.Models.AttendanceRecord>().Where(a => a.ClassID == roster.ClassID && a.StudentID == studentId).ToListAsync();
                int absences = attendance.Count(a => a.Status == "Absent" || a.Status == "A");
                int lates = attendance.Count(a => a.Status == "Late" || a.Status == "L");

                var perf = new StudentClassPerformanceViewModel
                {
                    SubjectName = tClass.SubjectName ?? "Unknown Subject",
                    Term = tClass.Term ?? "Unknown Term",
                    EducationMode = tClass.EducationMode ?? "Quarterly",
                    Absences = absences,
                    Lates = lates
                };

                // Helper func to convert the raw absolute percentage to the final string format
                string FormatGrade(double? raw)
                {
                    if (raw == null) return "--";
                    double val = raw.Value;
                    if (template?.CalculationMode == "NRFG")
                    {
                        val = (val / 100.0) * (100.0 - template.NrfgBaseValue) + template.NrfgBaseValue;
                    }
                    return $"{val.ToString("0.##")}%";
                }

                if (perf.EducationMode == "Semestral")
                {
                    double? mid = await CalculateTermGradeRawAsync(db, studentId, tClass.ClassID, tClass.GradingTemplateID, "Midterm");
                    double? fin = await CalculateTermGradeRawAsync(db, studentId, tClass.ClassID, tClass.GradingTemplateID, "Final");
                    
                    perf.MidtermGrade = FormatGrade(mid);
                    perf.FinalTermGrade = FormatGrade(fin);
                    perf.SemesterAverage = (mid != null && fin != null) ? FormatGrade((mid + fin) / 2.0) : "--";
                    perf.AverageScorePercentage = perf.SemesterAverage;
                }
                else // Quarterly
                {
                    double? q1 = await CalculateTermGradeRawAsync(db, studentId, tClass.ClassID, tClass.GradingTemplateID, "Q1");
                    double? q2 = await CalculateTermGradeRawAsync(db, studentId, tClass.ClassID, tClass.GradingTemplateID, "Q2");
                    double? q3 = await CalculateTermGradeRawAsync(db, studentId, tClass.ClassID, tClass.GradingTemplateID, "Q3");
                    double? q4 = await CalculateTermGradeRawAsync(db, studentId, tClass.ClassID, tClass.GradingTemplateID, "Q4");
                    
                    perf.Q1Grade = FormatGrade(q1);
                    perf.Q2Grade = FormatGrade(q2);
                    perf.Q3Grade = FormatGrade(q3);
                    perf.Q4Grade = FormatGrade(q4);
                    perf.FinalAverage = (q1 != null && q2 != null && q3 != null && q4 != null) ? FormatGrade((q1 + q2 + q3 + q4) / 4.0) : "--";
                    perf.AverageScorePercentage = perf.FinalAverage;
                }

                var allAssessments = await db.Table<Centriku.Models.Assessment>().Where(a => a.ClassID == roster.ClassID).ToListAsync();
                perf.GradedTasksCount = allAssessments.Count;

                performanceList.Add(perf);
            }

            SelectedStudentClasses = new System.Collections.ObjectModel.ObservableCollection<StudentClassPerformanceViewModel>(performanceList);
            SelectedStudentClassPerformance = performanceList.FirstOrDefault();
            HasEnrolledClasses = performanceList.Any();
        }

        private async Task<double?> CalculateTermGradeRawAsync(SQLite.SQLiteAsyncConnection db, string studentId, int classId, int templateId, string term)
        {
            var categories = await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == templateId).ToListAsync();
            var assessments = await db.Table<Centriku.Models.Assessment>().Where(a => a.ClassID == classId && a.GradingPeriod == term).ToListAsync();

            if (!assessments.Any()) return null;

            double totalWeightedScore = 0;
            double totalCategoryWeight = 0;
            bool hasAnyAssessments = false;

            foreach (var category in categories)
            {
                double weightDecimal = category.Weight / 100.0;
                totalCategoryWeight += weightDecimal;

                double catEarned = 0;
                double catMax = 0;

                var catAssessments = assessments.Where(a => a.Category == category.Name).ToList();
                foreach (var assessment in catAssessments)
                {
                    var score = await db.Table<Centriku.Models.Score>().Where(s => s.AssessmentID == assessment.AssessmentID && s.StudentID == studentId).FirstOrDefaultAsync();
                    if (score != null && !score.IsExcused && assessment.MaxScore > 0)
                    {
                        catEarned += score.PointsEarned;
                        catMax += assessment.MaxScore;
                    }
                }

                if (catMax > 0)
                {
                    hasAnyAssessments = true;
                    double catPercentage = (catEarned / catMax) * 100.0;
                    totalWeightedScore += (catPercentage * weightDecimal);
                }
            }

            if (!hasAnyAssessments || totalCategoryWeight == 0) return null;
            return totalWeightedScore / totalCategoryWeight;
        }
    
    }
    public partial class StudentRowViewModel(Centriku.Models.Student student) : ObservableObject
    {
      public Centriku.Models.Student DbModel { get; } = student;

      [ObservableProperty]
      public partial bool IsEditing { get; set; } = false;

      public string StudentID
        {
            get => DbModel.StudentID ?? string.Empty;
            set { DbModel.StudentID = value; OnPropertyChanged(); }
        }
        public string FirstName
        {
            get => DbModel.FirstName ?? string.Empty;
            set { DbModel.FirstName = value; OnPropertyChanged(); }
        }
        public string LastName
        {
            get => DbModel.LastName ?? string.Empty;
            set { DbModel.LastName = value; OnPropertyChanged(); }
        }
        public string MiddleName
        {
            get => DbModel.MiddleName ?? string.Empty;
            set { DbModel.MiddleName = value; OnPropertyChanged(); }
        }
        public string Suffix
        {
            get => DbModel.Suffix ?? string.Empty;
            set { DbModel.Suffix = value; OnPropertyChanged(); }
        }
        public string Gender
        {
            get => DbModel.Gender ?? string.Empty;
            set { DbModel.Gender = value; OnPropertyChanged(); }
        }
        
        public string GradeYearLevel
        {
            get => DbModel.GradeYearLevel ?? string.Empty;
            set { DbModel.GradeYearLevel = value; OnPropertyChanged(); }
        }
        public string SectionBlock
        {
            get => DbModel.SectionBlock ?? string.Empty;
            set { DbModel.SectionBlock = value; OnPropertyChanged(); }
        }
        public string EnrollmentStatus
        {
            get => DbModel.EnrollmentStatus ?? string.Empty;
            set { DbModel.EnrollmentStatus = value; OnPropertyChanged(); }
        }
   }

    public partial class StudentClassPerformanceViewModel : ObservableObject
    {
        public string SubjectName { get; set; } = string.Empty;
        public string Term { get; set; } = string.Empty;
        public string EducationMode { get; set; } = "Quarterly";
        
        public bool IsSemestralMode => EducationMode == "Semestral";
        public bool IsQuarterlyMode => EducationMode == "Quarterly";

        // Semestral Strings
        public string MidtermGrade { get; set; } = "--";
        public string FinalTermGrade { get; set; } = "--";
        public string SemesterAverage { get; set; } = "--";

        // Quarterly Strings
        public string Q1Grade { get; set; } = "--";
        public string Q2Grade { get; set; } = "--";
        public string Q3Grade { get; set; } = "--";
        public string Q4Grade { get; set; } = "--";
        public string FinalAverage { get; set; } = "--";

        public int GradedTasksCount { get; set; }
        public string AverageScorePercentage { get; set; } = "--";
        public int Absences { get; set; }
        public int Lates { get; set; }
    }
}