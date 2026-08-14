using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels
{
    public partial class GradebookViewModel : ViewModelBase
    {
        #region Core Properties & UI Toggles
            [ObservableProperty] public partial int SelectedTabIndex { get; set; } = 0;
            [ObservableProperty] public partial int ClassId { get; set; }
            [ObservableProperty] public partial string ClassTitle { get; set; } = string.Empty;        
            public System.Action<string>? ShowToastMessage { get; set; } 

            [ObservableProperty] public partial bool ShowStudentId { get; set; } = true;
            [ObservableProperty] public partial bool ShowFirstName { get; set; } = true;
            [ObservableProperty] public partial bool ShowLastName { get; set; } = true;
            [ObservableProperty] public partial bool ShowFinalGrade { get; set; } = true;
            [ObservableProperty] public partial bool ShowMidtermGrade { get; set; } = true;
            [ObservableProperty] public partial bool ShowFinalTermGrade { get; set; } = true;
            [ObservableProperty] public partial string CalculationMode { get; set; } = "Raw Percentage";
            [ObservableProperty] public partial double NrfgBaseValue { get; set; } = 60.0;
            public System.Collections.Generic.List<Centriku.Models.GradeBoundary> ClassGradeBoundaries { get; set; } = new();
            
            partial void OnShowStudentIdChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
            partial void OnShowFirstNameChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
            partial void OnShowLastNameChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
            partial void OnShowFinalGradeChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
            partial void OnShowMidtermGradeChanged(bool value) { TriggerGridRedraw(); }
            partial void OnShowFinalTermGradeChanged(bool value) { TriggerGridRedraw(); }

            [ObservableProperty] public partial int GridRefreshTrigger { get; set; } = 0;
            private void TriggerGridRedraw() => GridRefreshTrigger++;

            private async void SaveClassSettings()
            {
                var db = new DatabaseService().GetConnection();
                var currentClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
                if (currentClass != null)
                {
                    currentClass.ShowStudentId = ShowStudentId;
                    currentClass.ShowFirstName = ShowFirstName;
                    currentClass.ShowLastName = ShowLastName;
                    currentClass.ShowFinalGrade = ShowFinalGrade;
                    currentClass.ShowTotalP = ShowTotalP;
                    currentClass.ShowTotalL = ShowTotalL;
                    currentClass.ShowTotalA = ShowTotalA;
                    currentClass.AttendanceCalculationMode = AttendanceCalculationMode;
                    currentClass.MaxAbsencesAllowed = MaxAbsencesAllowed;
                    currentClass.AttendanceWeight = AttendanceWeight;
                    currentClass.LateValue = LateValue;

                    await db.UpdateAsync(currentClass);
                }
            }
     
            [ObservableProperty] public partial ObservableCollection<string> TermViews { get; set; } = new();
            [ObservableProperty] public partial ObservableCollection<string> GradingPeriods { get; set; } = new();
            
            [ObservableProperty] public partial string SelectedTermView { get; set; } = string.Empty;
            
            public bool ShowAssessmentFilters => SelectedTermView != "Semester Average" && SelectedTermView != "Final Average";
            public bool IsSemesterAverageView => SelectedTermView == "Semester Average" || SelectedTermView == "Final Average";
            public string DynamicFinalColumnName => IsSemesterAverageView ? SelectedTermView : $"{SelectedTermView} Grade";
            
            partial void OnSelectedTermViewChanged(string value) 
            { 
                OnPropertyChanged(nameof(ShowAssessmentFilters)); 
                OnPropertyChanged(nameof(IsSemesterAverageView));
                OnPropertyChanged(nameof(DynamicFinalColumnName));
                BuildCategoryFilters();
                TriggerGridRedraw(); 
                RecalculateFinalGrades(); 
            }

            [ObservableProperty] public partial AttendanceCellViewModel? SelectedAttendanceCell { get; set; }
            [ObservableProperty] public partial string SelectedAttendanceStudentName { get; set; } = string.Empty;
            [ObservableProperty] public partial string SelectedAttendanceDateDisplay { get; set; } = string.Empty;
            [ObservableProperty] public partial bool IsAttendancePanelOpen { get; set; } = false;
            [RelayCommand] public void CloseAttendancePanel() => IsAttendancePanelOpen = false;
        #endregion

        #region Grid Data Collections
            [ObservableProperty] public partial ObservableCollection<StudentGradeRow> GradebookRows { get; set; } = new();   
            [ObservableProperty] public partial ObservableCollection<Assessment> ClassAssessments { get; set; } = new();
            [ObservableProperty] public partial ObservableCollection<AttendanceGridRowViewModel> AttendanceGridRows { get; set; } = new();
            [ObservableProperty] public partial ObservableCollection<System.DateTime> AttendanceDates { get; set; } = new();
            [ObservableProperty] public partial ObservableCollection<GradingCategory> AvailableCategories { get; set; } = new();
            [ObservableProperty] public partial GradingCategory? SelectedCategory { get; set; }
            [ObservableProperty] public partial ObservableCollection<CategoryFilterViewModel> CategoryFilters { get; set; } = new();
        
            private void BuildCategoryFilters()
            {
                if (ClassAssessments == null) return;
                CategoryFilters.Clear();
                // If viewing the Semester Average, the flyout hides assessments anyway, so stop here!
                if (SelectedTermView == "Semester Average") return; 

                // Filter the assessments so the flyout ONLY shows quizzes for the currently viewed term!
                var relevantAssessments = ClassAssessments.Where(a => a.GradingPeriod == SelectedTermView).ToList();
                var allFilters = relevantAssessments.Select(a => new AssessmentFilterViewModel(a, TriggerGridRedraw)).ToList();
                var grouped = allFilters.GroupBy(f => f.DbModel.Category ?? "Uncategorized");

                foreach (var group in grouped)
                { CategoryFilters.Add(new CategoryFilterViewModel(group.Key, group)); }
            }
        #endregion

        #region Setup& Data Loading

            [ObservableProperty] public partial bool ExportQ1 { get; set; } = true;
            [ObservableProperty] public partial bool ExportQ2 { get; set; } = true;
            [ObservableProperty] public partial bool ExportQ3 { get; set; } = true;
            [ObservableProperty] public partial bool ExportQ4 { get; set; } = true;
            [ObservableProperty] public partial bool ExportFinalAverage { get; set; } = true;
            [ObservableProperty] public partial bool ExportMidterm { get; set; } = true;
            [ObservableProperty] public partial bool ExportFinal { get; set; } = true;
            [ObservableProperty] public partial bool ExportSemesterAverage { get; set; } = true;
            [ObservableProperty] public partial bool ExportAttendance { get; set; } = true;
            [ObservableProperty] public partial string ExportFolderPath { get; set; } = string.Empty;
            [ObservableProperty] public partial string ExportFolderDisplay { get; set; } = "Default Downloads Folder";
            public IRelayCommand ExportCsvCommand { get; }
            private async void ExportToCsv()
            {
                ShowToastMessage?.Invoke("Generating Official Excel File...");

                var db = new DatabaseService().GetConnection();
                
                // 1. Fetch the TeacherClass info
                var currentClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
                if (currentClass == null) return;

                // 2. Fetch the Students (Using the active GradebookRows)
                var studentsToExport = new System.Collections.Generic.List<Student>();
                foreach (var row in GradebookRows)
                {
                    studentsToExport.Add(row.StudentInfo);
                }

                // 3. Send data to the Excel Service
                var result = await ExcelExportService.ExportToNwSSUTemplateAsync(currentClass, studentsToExport, ExportFolderPath);
                
                ShowToastMessage?.Invoke(result.Message); 
            }

            private async Task<(System.Collections.Generic.List<StudentGradeRow> Grades, System.Collections.Generic.List<AttendanceGridRowViewModel> Attendance)> FetchArchivedExportDataAsync()
            {
                var db = new DatabaseService().GetConnection();
                var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
                var studentIds = roster.Select(r => r.StudentID).ToList();

                // 1. Find ONLY the Archived/Dropped students for this class
                var archivedStudents = (await db.Table<Student>().Where(s => studentIds.Contains(s.StudentID)).ToListAsync())
                                       .Where(s => s.IsArchived || s.EnrollmentStatus == "Dropped").ToList();

                var archivedGrades = new System.Collections.Generic.List<StudentGradeRow>();
                var archivedAtt = new System.Collections.Generic.List<AttendanceGridRowViewModel>();

                if (!archivedStudents.Any()) return (archivedGrades, archivedAtt);

                var scores = await db.Table<Score>().Where(s => studentIds.Contains(s.StudentID)).ToListAsync();
                var attendance = await db.Table<AttendanceRecord>().Where(a => a.ClassID == ClassId).ToListAsync();

                // 2. Build their invisible rows
                foreach (var student in archivedStudents)
                {
                    // Add a visual tag to their name so the teacher knows WHY they are at the bottom of the CSV!
                    student.LastName = $"[ARCHIVED] {student.LastName}";

                    var gradeRow = new StudentGradeRow(student);
                    var studentScores = scores.Where(s => s.StudentID == student.StudentID).ToList();
                    
                    foreach (var assessment in ClassAssessments)
                    {
                        var existingScore = studentScores.FirstOrDefault(s => s.AssessmentID == assessment.AssessmentID);
                        if (existingScore != null) 
                            gradeRow.Scores[assessment.AssessmentID] = new ScoreCellViewModel(existingScore, assessment.MaxScore, () => {});
                        else 
                            gradeRow.Scores[assessment.AssessmentID] = new ScoreCellViewModel(new Score { AssessmentID = assessment.AssessmentID, StudentID = student.StudentID, PointsEarned = 0 }, assessment.MaxScore, () => {});
                    }
                    archivedGrades.Add(gradeRow);

                    var attRow = new AttendanceGridRowViewModel(student);
                    var studentAtt = attendance.Where(a => a.StudentID == student.StudentID).ToList();
                    
                    foreach (var date in AttendanceDates)
                    {
                        var existingAtt = studentAtt.FirstOrDefault(a => a.Date.Date == date);
                        if (existingAtt == null) existingAtt = new AttendanceRecord { Status = "" };
                        attRow.Cells[date.ToString("yyyy-MM-dd")] = new AttendanceCellViewModel(existingAtt, () => {}, msg => {});
                    }
                    archivedAtt.Add(attRow);
                }

                // 3. Run the Math Engine strictly on these invisible rows!
                RecalculateFinalGradesForList(archivedGrades, archivedAtt);

                return (archivedGrades, archivedAtt);
            }

            public GradebookViewModel()
            {
                ToggleEnrollmentCommand = new RelayCommand(ToggleEnrollment);
                SaveEnrollmentCommand = new RelayCommand(SaveEnrollment);
                RemoveStudentCommand = new RelayCommand<Student>(RemoveStudent!);

                ToggleAddAssessmentCommand = new RelayCommand(() => 
                {
                    if (IsAddingAssessment) ResetAssessmentForm(); // If clicking Cancel, wipe everything clean!
                    else IsAddingAssessment = true;                // If clicking Add, just open it.
                });

                SaveAssessmentCommand = new RelayCommand(SaveAssessment);

                EditAssessmentCommand = new RelayCommand<Assessment>(EditAssessment!);
                DeleteAssessmentCommand = new RelayCommand<Assessment>(DeleteAssessment!);

                ToggleAddRollCallCommand = new RelayCommand(() => 
                {
                    if (IsAddingRollCall) ResetRollCallForm();
                    else IsAddingRollCall = true;
                });
                SaveRollCallCommand = new RelayCommand(SaveRollCallDay);
                EditRollCallCommand = new RelayCommand<System.DateTime?>(EditRollCall);
                DeleteRollCallCommand = new RelayCommand<System.DateTime?>(DeleteRollCall);
                ExportCsvCommand = new RelayCommand(ExportToCsv);
            }
            public async void Initialize(int classId, string classTitle, int startingTab = 0)
            {
                ClassId = classId;
                ClassTitle = classTitle;
                SelectedTabIndex = startingTab;
                await LoadGradebookData();
                await LoadCategories();
                await LoadAttendanceData();
            }

            public async Task RefreshRostersAsync()
            {
                await LoadGradebookData();
                await LoadAttendanceData();
            }
            private async Task LoadGradebookData()
            {
                var db = new DatabaseService().GetConnection();
                await db.CreateTableAsync<Centriku.Models.GradeBoundary>();
                // 1. Load Class Visibility Settings 
                var currentClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
                if (currentClass != null)
                {
                    ShowStudentId = currentClass.ShowStudentId;
                    ShowFirstName = currentClass.ShowFirstName;
                    ShowLastName = currentClass.ShowLastName;
                    ShowFinalGrade = currentClass.ShowFinalGrade;
                    AttendanceCalculationMode = currentClass.AttendanceCalculationMode ?? "None";
                    MaxAbsencesAllowed = currentClass.MaxAbsencesAllowed;
                    AttendanceWeight = currentClass.AttendanceWeight;
                    LateValue = currentClass.LateValue;
                    var template = await db.Table<GradingTemplate>().Where(t => t.TemplateID == currentClass.GradingTemplateID).FirstOrDefaultAsync();
                    if (template != null) 
                    {
                        CalculationMode = template.CalculationMode ?? "NRFG";
                        NrfgBaseValue = template.NrfgBaseValue; 
                    }
                    
                    ClassGradeBoundaries = await db.Table<GradeBoundary>().Where(b => b.TemplateID == currentClass.GradingTemplateID).ToListAsync();
                }

                TermViews = new ObservableCollection<string> { "Midterm", "Final", "Semester Average" };
                GradingPeriods = new ObservableCollection<string> { "Midterm", "Final" };
                if (!TermViews.Contains(SelectedTermView)) SelectedTermView = "Semester Average";

                // 2. Get the Columns (Assessments)
                var assessments = await db.Table<Assessment>().Where(a => a.ClassID == ClassId).ToListAsync();
                ClassAssessments = new ObservableCollection<Assessment>(assessments);

                BuildCategoryFilters();

                // 2. Get the Students in this Class
                var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
                var studentIds = roster.Select(r => r.StudentID).ToList();
                var enrolled = (await db.Table<Student>().Where(s => studentIds.Contains(s.StudentID)).ToListAsync()).Where(s => !s.IsArchived && s.EnrollmentStatus != "Dropped").ToList();

                // 3. Get the Scores for this Class
                var assessmentIds = assessments.Select(a => a.AssessmentID).ToList();
                var scores = await db.Table<Score>().Where(s => assessmentIds.Contains(s.AssessmentID)).ToListAsync();

                // 4. Stitch them together into Rows!
                GradebookRows.Clear();
                foreach (var student in enrolled)
                {
                    var row = new StudentGradeRow(student);
                    
                    // Find all existing scores belonging to this specific student
                    var studentScores = scores.Where(s => s.StudentID == student.StudentID).ToList();

                    foreach (var assessment in ClassAssessments)
                    {
                        // Check if the student already has a saved score for this column
                        var existingScore = studentScores.FirstOrDefault(s => s.AssessmentID == assessment.AssessmentID);
                        
                        if (existingScore != null)
                        {
                            // Wrap the existing score
                            row.Scores[assessment.AssessmentID] = new ScoreCellViewModel(existingScore, assessment.MaxScore, RecalculateFinalGrades);
                        }
                        else
                        {
                            var blankScore = new Score 
                            { 
                                AssessmentID = assessment.AssessmentID, 
                                StudentID = student.StudentID, 
                                PointsEarned = 0 
                            };
                            row.Scores[assessment.AssessmentID] = new ScoreCellViewModel(blankScore, assessment.MaxScore, RecalculateFinalGrades);
                        }
                    }
                    
                    GradebookRows.Add(row);
                }
                TriggerGridRedraw();
                RecalculateFinalGrades();
            }
            private async Task LoadAttendanceData()
            {
                var db = new DatabaseService().GetConnection();
                await db.CreateTableAsync<AttendanceRecord>(); 
                
                // 1. Load Settings Memory
                var currentClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
                if (currentClass != null)
                {
                    ShowTotalP = currentClass.ShowTotalP;
                    ShowTotalL = currentClass.ShowTotalL;
                    ShowTotalA = currentClass.ShowTotalA;
                }

                // 2. Get students & ALL attendance records
                var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
                var studentIds = roster.Select(r => r.StudentID).ToList();
                var enrolled = (await db.Table<Student>().Where(s => studentIds.Contains(s.StudentID)).ToListAsync()).Where(s => !s.IsArchived && s.EnrollmentStatus != "Dropped").OrderBy(s => s.LastName).ToList();
                var allRecords = await db.Table<AttendanceRecord>().Where(a => a.ClassID == ClassId).ToListAsync();

                // 3. Find unique dates to build our Columns
                var uniqueDates = allRecords.Select(r => r.Date.Date).Distinct().OrderBy(d => d).ToList();
                AttendanceDates = new ObservableCollection<System.DateTime>(uniqueDates);

                var extractedMonths = uniqueDates.Select(d => d.ToString("MMM yyyy")).Distinct().ToList();
                
                AvailableMonths.Clear();
                AvailableMonths.Add("All Months"); // Always keep an "All" option at the top!
                
                foreach (var m in extractedMonths)
                {
                    AvailableMonths.Add(m);
                }
                
                // Safety check: If the teacher deleted a date and that month no longer exists, reset the filter
                if (!AvailableMonths.Contains(SelectedMonthFilter)) 
                {
                    SelectedMonthFilter = "All Months";
                }

                // 4. Build the Excel Rows
                AttendanceGridRows.Clear();
                foreach (var student in enrolled)
                {
                    var row = new AttendanceGridRowViewModel(student);
                    var studentRecords = allRecords.Where(r => r.StudentID == student.StudentID).ToList();

                    foreach (var date in uniqueDates)
                    {
                        var existingRecord = studentRecords.FirstOrDefault(r => r.Date.Date == date);
                        if (existingRecord == null) existingRecord = new AttendanceRecord { ClassID = ClassId, StudentID = student.StudentID, Date = date, Status = "" };
                        
                        row.Cells[date.ToString("yyyy-MM-dd")] = new AttendanceCellViewModel(
                            existingRecord, 
                            () => 
                            {
                                row.RefreshTotals();
                                RecalculateFinalGrades(); 
                            }, 
                            msg => ShowToastMessage?.Invoke(msg)
                        );
                    }
                    AttendanceGridRows.Add(row);
                }
                TriggerGridRedraw(); 
                RecalculateFinalGrades();
            }
            private async Task LoadCategories()
            {
                var db = new DatabaseService().GetConnection();
                
                // 1. Get the current class to find out which Template it uses
                var currentClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
                
                if (currentClass != null)
                {
                    // 2. Fetch only the categories that belong to that specific template!
                    var categories = await db.Table<GradingCategory>().Where(cat => cat.TemplateID == currentClass.GradingTemplateID).ToListAsync();
                    AvailableCategories = new ObservableCollection<GradingCategory>(categories);
                }
            }

        #endregion

    }
}