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
        [ObservableProperty] public partial bool IsMasterRecordVisible { get; set; } = true;
        [ObservableProperty] public partial ObservableCollection<MasterGradeRowViewModel> MasterGrades { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<Sf9MonthlyAttendance> Sf9Attendance { get; set; } = new(); 
        // The 5 Quarterly Average Trackers
        [ObservableProperty] public partial string Q1Average { get; set; } = "--";
        [ObservableProperty] public partial string Q2Average { get; set; } = "--";
        [ObservableProperty] public partial string Q3Average { get; set; } = "--";
        [ObservableProperty] public partial string Q4Average { get; set; } = "--";
        [ObservableProperty] public partial string FinalGeneralAverage { get; set; } = "--";

        public IRelayCommand SaveMasterRecordCommand { get; }
        public IRelayCommand AddBlankMasterSubjectCommand { get; }
        public IRelayCommand<MasterGradeRowViewModel> DeleteMasterSubjectCommand { get; } 
        public IRelayCommand GenerateSf9Command { get; }
        
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
            SaveMasterRecordCommand = new RelayCommand(SaveMasterRecord);
            AddBlankMasterSubjectCommand = new RelayCommand(AddBlankMasterSubject);
            DeleteMasterSubjectCommand = new RelayCommand<MasterGradeRowViewModel>(DeleteMasterSubject!);
            GenerateSf9Command = new RelayCommand(GenerateSf9);
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

                string FormatGrade(double? raw)
                {
                    if (raw == null) return "--";
                    double val = raw.Value;
                    if (template?.CalculationMode == "NRFG") val = (val / 100.0) * (100.0 - template.NrfgBaseValue) + template.NrfgBaseValue;
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
                else 
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

            // --- MASTER ACADEMIC RECORD LOGIC ---
            bool hasSemestral = performanceList.Any(p => p.EducationMode == "Semestral");
            bool hasQuarterly = performanceList.Any(p => p.EducationMode == "Quarterly");

            // Hide the SF9 Tab ONLY if they are exclusively enrolled in Semestral/College classes!
            IsMasterRecordVisible = !(hasSemestral && !hasQuarterly);

            if (IsMasterRecordVisible) await LoadMasterRecordAsync(studentId, performanceList);

            // --- LOAD ATTENDANCE FOR SF9 ---
            var allAttendance = await db.Table<Centriku.Models.AttendanceRecord>()
                                        .Where(a => a.StudentID == studentId)
                                        .ToListAsync();

            var sf9AttList = new List<Sf9MonthlyAttendance>();
            int[] monthOrder = [8, 9, 10, 11, 12, 1, 2, 3, 4, 5]; 
            string[] monthNames = ["Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr", "May"];

            for (int i = 0; i < monthOrder.Length; i++)
            {
                int m = monthOrder[i];
                var monthRecords = allAttendance.Where(a => a.Date.Month == m).ToList();
                
                // Group by the specific Day so we don't double-count students taking multiple subjects
                var uniqueDates = monthRecords.GroupBy(a => a.Date.Date).ToList();

                int present = 0;
                int absent = 0;

                foreach (var dateGroup in uniqueDates)
                {
                    // If marked Present or Late in ANY subject that day, they are present for the school day.
                    if (dateGroup.Any(r => r.Status == "P" || r.Status == "L"))
                    {
                        present++;
                    }
                    else // If all their records for that day are Absent (A) or Excused (E), they are absent.
                    {
                        absent++;
                    }
                }

                sf9AttList.Add(new Sf9MonthlyAttendance
                {
                    Month = monthNames[i],
                    MonthNum = m,
                    DaysPresent = present,
                    DaysAbsent = absent,
                    SchoolDays = present + absent
                });
            }
            Sf9Attendance = new ObservableCollection<Sf9MonthlyAttendance>(sf9AttList);
        }

        private async Task LoadMasterRecordAsync(string studentId, List<StudentClassPerformanceViewModel> activeClasses)
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.CreateTableAsync<Centriku.Models.MasterQuarterlyGrade>();
            var savedGrades = await db.Table<Centriku.Models.MasterQuarterlyGrade>().Where(g => g.StudentID == studentId).ToListAsync();

            var masterList = new List<MasterGradeRowViewModel>();

            // 1. Auto-pull Quarterly classes (Ignore Semestral entirely!)
            var activeQuarterly = activeClasses.Where(c => c.EducationMode == "Quarterly").ToList();
            foreach(var aq in activeQuarterly)
            {
                string cleanVal(string val) => val.Replace("%", "").Trim();
                var row = new MasterGradeRowViewModel 
                {
                    SubjectName = aq.SubjectName,
                    Q1Text = cleanVal(aq.Q1Grade), Q2Text = cleanVal(aq.Q2Grade), Q3Text = cleanVal(aq.Q3Grade), Q4Text = cleanVal(aq.Q4Grade),
                    IsFromActiveGradebook = true, // Lock editing!
                    TriggerParentRecalc = RecalculateGeneralAverage
                };
                row.ForceRecalc();
                masterList.Add(row);
            }

            // 2. Add manually encoded external subjects from SQLite
            foreach(var sg in savedGrades)
            {
                var row = new MasterGradeRowViewModel
                {
                    GradeId = sg.GradeID,
                    SubjectName = sg.SubjectName ?? "Unknown",
                    Q1Text = sg.Quarter1Grade?.ToString() ?? "", Q2Text = sg.Quarter2Grade?.ToString() ?? "", Q3Text = sg.Quarter3Grade?.ToString() ?? "", Q4Text = sg.Quarter4Grade?.ToString() ?? "",
                    IsFromActiveGradebook = false,
                    TriggerParentRecalc = RecalculateGeneralAverage
                };
                row.ForceRecalc();
                masterList.Add(row);
            }

            MasterGrades = new ObservableCollection<MasterGradeRowViewModel>(masterList);
            RecalculateGeneralAverage();
        }

        private void RecalculateGeneralAverage()
        {
            // Helper function to safely average a specific quarter across all subjects
            double CalculateColumnAverage(Func<MasterGradeRowViewModel, string> selector)
            {
                var validRows = MasterGrades.Where(r => double.TryParse(selector(r), out _)).ToList();
                if (!validRows.Any()) return -1;
                return validRows.Sum(r => double.Parse(selector(r))) / validRows.Count;
            }

            double q1 = CalculateColumnAverage(r => r.Q1Text);
            double q2 = CalculateColumnAverage(r => r.Q2Text);
            double q3 = CalculateColumnAverage(r => r.Q3Text);
            double q4 = CalculateColumnAverage(r => r.Q4Text);
            double fin = CalculateColumnAverage(r => r.FinalGrade);

            Q1Average = q1 >= 0 ? q1.ToString("0.##") : "--";
            Q2Average = q2 >= 0 ? q2.ToString("0.##") : "--";
            Q3Average = q3 >= 0 ? q3.ToString("0.##") : "--";
            Q4Average = q4 >= 0 ? q4.ToString("0.##") : "--";
            FinalGeneralAverage = fin >= 0 ? fin.ToString("0.##") : "--";
        }

        private async void DeleteMasterSubject(MasterGradeRowViewModel row)
        {
            if (row == null || row.IsFromActiveGradebook) return; // Prevent deleting app-controlled classes!

            MasterGrades.Remove(row); // 1. Remove from UI instantly
            
            if (row.GradeId != 0) // 2. If it was previously saved, delete it from SQLite
            {
                var db = new Centriku.Services.DatabaseService().GetConnection();
                var dbRecord = await db.Table<Centriku.Models.MasterQuarterlyGrade>().Where(g => g.GradeID == row.GradeId).FirstOrDefaultAsync();
                if (dbRecord != null)
                {
                    await db.DeleteAsync(dbRecord);
                }
            }
            RecalculateGeneralAverage(); // 3. Fix the averages!
        }
        private void AddBlankMasterSubject() => MasterGrades.Add(new MasterGradeRowViewModel { SubjectName = "New Subject", IsFromActiveGradebook = false, TriggerParentRecalc = RecalculateGeneralAverage });

        private async void SaveMasterRecord()
        {
            if (SelectedProfile == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var existingRecords = await db.Table<Centriku.Models.MasterQuarterlyGrade>().Where(g => g.StudentID == SelectedProfile.StudentID).ToListAsync();

            foreach(var row in MasterGrades)
            {
                if (row.IsFromActiveGradebook) continue; // App data manages itself!

                var dbRecord = existingRecords.FirstOrDefault(e => e.GradeID == row.GradeId);
                if (dbRecord != null)
                {
                    dbRecord.SubjectName = row.SubjectName;
                    dbRecord.Quarter1Grade = double.TryParse(row.Q1Text, out double q1) ? q1 : null;
                    dbRecord.Quarter2Grade = double.TryParse(row.Q2Text, out double q2) ? q2 : null;
                    dbRecord.Quarter3Grade = double.TryParse(row.Q3Text, out double q3) ? q3 : null;
                    dbRecord.Quarter4Grade = double.TryParse(row.Q4Text, out double q4) ? q4 : null;
                    await db.UpdateAsync(dbRecord);
                }
                else
                {
                    var newRec = new Centriku.Models.MasterQuarterlyGrade
                    {
                        StudentID = SelectedProfile.StudentID, SubjectName = row.SubjectName,
                        Quarter1Grade = double.TryParse(row.Q1Text, out double nq1) ? nq1 : null,
                        Quarter2Grade = double.TryParse(row.Q2Text, out double nq2) ? nq2 : null,
                        Quarter3Grade = double.TryParse(row.Q3Text, out double nq3) ? nq3 : null,
                        Quarter4Grade = double.TryParse(row.Q4Text, out double nq4) ? nq4 : null
                    };
                    await db.InsertAsync(newRec);
                    row.GradeId = newRec.GradeID; 
                }
            }
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
    
        private void GenerateSf9()
        {
            if (SelectedProfile == null) return;

            // Create a safe file name (e.g., "Mason_Justin_SF9.pdf")
            string safeName = $"{SelectedProfile.LastName}_{SelectedProfile.FirstName}_SF9".Replace(" ", "_");
            
            // Save it directly to the user's Desktop for easy access
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string fullPath = System.IO.Path.Combine(desktopPath, $"{safeName}.pdf");

            try
            {
                // We pass 'this' (the entire ViewModel) so the generator can access MasterGrades!
                Centriku.Services.Sf9Generator.GenerateReportCard(this, fullPath);
                
                // Automatically open the PDF immediately after generating it
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to generate PDF: {ex.Message}");
            }
        }

        public class Sf9MonthlyAttendance
        {
            public string Month { get; set; } = string.Empty;
            public int MonthNum { get; set; }
            public int SchoolDays { get; set; }
            public int DaysPresent { get; set; }
            public int DaysAbsent { get; set; }
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

    public partial class MasterGradeRowViewModel : ObservableObject
    {
        public int GradeId { get; set; } 
        public bool IsFromActiveGradebook { get; set; } // If true, it locks the UI so you can't overwrite app data!
        
        [ObservableProperty] public partial string SubjectName { get; set; } = string.Empty;
        
        // We use strings for inputs so the Avalonia TextBoxes behave perfectly without crashing on empty values
        [ObservableProperty] public partial string Q1Text { get; set; } = string.Empty;
        [ObservableProperty] public partial string Q2Text { get; set; } = string.Empty;
        [ObservableProperty] public partial string Q3Text { get; set; } = string.Empty;
        [ObservableProperty] public partial string Q4Text { get; set; } = string.Empty;

        partial void OnQ1TextChanged(string value) { UpdateMath(); }
        partial void OnQ2TextChanged(string value) { UpdateMath(); }
        partial void OnQ3TextChanged(string value) { UpdateMath(); }
        partial void OnQ4TextChanged(string value) { UpdateMath(); }

        [ObservableProperty] public partial string FinalGrade { get; set; } = "--";
        [ObservableProperty] public partial string Remarks { get; set; } = "--";

        public Action? TriggerParentRecalc { get; set; }

        public void ForceRecalc() => UpdateMath();

        private void UpdateMath()
        {
            // If all 4 quarters have numbers typed in, calculate the Final Rating automatically!
            if (double.TryParse(Q1Text, out double q1) &&
                double.TryParse(Q2Text, out double q2) &&
                double.TryParse(Q3Text, out double q3) &&
                double.TryParse(Q4Text, out double q4))
            {
                double avg = (q1 + q2 + q3 + q4) / 4.0;
                FinalGrade = Math.Round(avg, 0).ToString("0.##"); // Standard K-12 Rounding
                Remarks = avg >= 75 ? "Passed" : "Failed";
            }
            else
            {
                FinalGrade = "--";
                Remarks = "--";
            }
            TriggerParentRecalc?.Invoke(); // Tell the General Average at the bottom of the screen to update!
        }
    }
}