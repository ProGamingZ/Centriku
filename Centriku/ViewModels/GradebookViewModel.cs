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
            [ObservableProperty] public partial int ClassId { get; set; }
            [ObservableProperty] public partial string ClassTitle { get; set; } = string.Empty;        
            public System.Action<string>? ShowToastMessage { get; set; } 

            [ObservableProperty] public partial bool ShowLRN { get; set; } = true;
            [ObservableProperty] public partial bool ShowFirstName { get; set; } = true;
            [ObservableProperty] public partial bool ShowLastName { get; set; } = true;
            [ObservableProperty] public partial bool ShowFinalGrade { get; set; } = true;
            partial void OnShowLRNChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
            partial void OnShowFirstNameChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
            partial void OnShowLastNameChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
            partial void OnShowFinalGradeChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }

            [ObservableProperty] public partial int GridRefreshTrigger { get; set; } = 0;
            private void TriggerGridRedraw() => GridRefreshTrigger++;

            private async void SaveClassSettings()
            {
                var db = new DatabaseService().GetConnection();
                var currentClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
                if (currentClass != null)
                {
                    currentClass.ShowLRN = ShowLRN;
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

            [ObservableProperty] public partial ObservableCollection<string> TermViews { get; set; } = new() { "Midterm", "Final", "Semester Average" };
            [ObservableProperty] public partial string SelectedTermView { get; set; } = "Semester Average";
            public bool ShowAssessmentFilters => SelectedTermView != "Semester Average";
            partial void OnSelectedTermViewChanged(string value) 
            { 
                OnPropertyChanged(nameof(ShowAssessmentFilters)); // Tell UI to hide/show the list
                BuildCategoryFilters();
                TriggerGridRedraw(); 
                RecalculateFinalGrades(); 
            }

            [ObservableProperty] public partial ObservableCollection<string> GradingPeriods { get; set; } = new() { "Midterm", "Final" };
            [ObservableProperty] public partial string NewAssessmentPeriod { get; set; } = "Midterm";
        #endregion

        #region Attendance Policy
            public System.Collections.ObjectModel.ObservableCollection<string> AttendanceModes { get; } = new() { "None", "Threshold", "Weighted", "Bonus" };
            [ObservableProperty] public partial string AttendanceCalculationMode { get; set; } = "None";
            [ObservableProperty] public partial int MaxAbsencesAllowed { get; set; } = 3;
            [ObservableProperty] public partial double AttendanceWeight { get; set; } = 10.0;
            [ObservableProperty] public partial double LateValue { get; set; } = 0.5;

            public bool IsThresholdMode => AttendanceCalculationMode == "Threshold";
            public bool IsWeightedOrBonusMode => AttendanceCalculationMode == "Weighted" || AttendanceCalculationMode == "Bonus";
            public bool IsMathEngineActive => AttendanceCalculationMode != "None";

            partial void OnAttendanceCalculationModeChanged(string value) 
            { 
                OnPropertyChanged(nameof(IsThresholdMode)); 
                OnPropertyChanged(nameof(IsWeightedOrBonusMode)); 
                OnPropertyChanged(nameof(IsMathEngineActive)); 
                SaveClassSettings(); 
                RecalculateFinalGrades();
            }
            partial void OnMaxAbsencesAllowedChanged(int value) { SaveClassSettings(); RecalculateFinalGrades();}
            partial void OnAttendanceWeightChanged(double value) { SaveClassSettings(); RecalculateFinalGrades();}
            partial void OnLateValueChanged(double value) { SaveClassSettings(); RecalculateFinalGrades();}
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

            public IRelayCommand ExportCsvCommand { get; }
            private async void ExportToCsv()
            {
                ShowToastMessage?.Invoke("Generating CSV files...");

                var result = await CsvExportService.ExportClassDataAsync(
                    ClassTitle,
                    GradebookRows,
                    ClassAssessments,
                    AttendanceGridRows,
                    AttendanceDates
                );
                // Pop the toast letting the teacher know exactly where it saved!
                ShowToastMessage?.Invoke(result.Message); 
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
            public async void Initialize(int classId, string classTitle)
            {
                ClassId = classId;
                ClassTitle = classTitle;
                await LoadGradebookData();
                await LoadCategories();
                await LoadAttendanceData();
            }
            private async Task LoadGradebookData()
            {
                var db = new DatabaseService().GetConnection();

                // 1. Load Class Visibility Settings 
                var currentClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
                if (currentClass != null)
                {
                    ShowLRN = currentClass.ShowLRN;
                    ShowFirstName = currentClass.ShowFirstName;
                    ShowLastName = currentClass.ShowLastName;
                    ShowFinalGrade = currentClass.ShowFinalGrade;
                    AttendanceCalculationMode = currentClass.AttendanceCalculationMode ?? "None";
                    MaxAbsencesAllowed = currentClass.MaxAbsencesAllowed;
                    AttendanceWeight = currentClass.AttendanceWeight;
                    LateValue = currentClass.LateValue;
                }

                // 2. Get the Columns (Assessments)
                var assessments = await db.Table<Assessment>().Where(a => a.ClassID == ClassId).ToListAsync();
                ClassAssessments = new ObservableCollection<Assessment>(assessments);

                BuildCategoryFilters();

                // 2. Get the Students in this Class
                var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
                var studentIds = roster.Select(r => r.StudentID).ToList();
                var enrolled = await db.Table<Student>().Where(s => studentIds.Contains(s.StudentID)).ToListAsync();

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
                var enrolled = await db.Table<Student>().Where(s => studentIds.Contains(s.StudentID)).OrderBy(s => s.LastName).ToListAsync();
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

        #region Enrollment Module
            [ObservableProperty] public partial bool IsEnrolling { get; set; } = false;
            [ObservableProperty] public partial ObservableCollection<EnrollmentItemViewModel> AvailableStudents { get; set; } = new();
            public IRelayCommand ToggleEnrollmentCommand { get; }
            public IRelayCommand SaveEnrollmentCommand { get; }
            public IRelayCommand<Student> RemoveStudentCommand { get; }

            private double CalculateAcademicGrade(StudentGradeRow row, string targetPeriod)
            {
                double academicGrade = 100.0; 
                double totalWeightedScore = 0;
                double totalActiveWeight = 0;

                if (AvailableCategories != null && ClassAssessments != null)
                {
                    foreach (var category in AvailableCategories)
                    {
                        double catEarned = 0;
                        double catMax = 0;

                        // ONLY grab assessments that belong to this category AND this specific Grading Period
                        var categoryAssessments = ClassAssessments.Where(a => a.Category == category.Name && a.GradingPeriod == targetPeriod).ToList();

                        foreach (var assessment in categoryAssessments)
                        {
                            if (row.Scores.TryGetValue(assessment.AssessmentID, out var cell))
                            {
                                catEarned += cell.PointsEarned;
                                catMax += assessment.MaxScore;
                            }
                        }

                        if (catMax > 0)
                        {
                            double catPercentage = (catEarned / catMax) * 100.0;
                            double weightDecimal = category.Weight / 100.0;

                            totalWeightedScore += (catPercentage * weightDecimal);
                            totalActiveWeight += weightDecimal; 
                        }
                    }

                    if (totalActiveWeight > 0) academicGrade = totalWeightedScore / totalActiveWeight;
                }
                return academicGrade;
            }
            public void RecalculateFinalGrades()
            {
                if (GradebookRows == null || AttendanceGridRows == null) return;

                foreach (var row in GradebookRows)
                {
                    // === 1. TERM AVERAGING MATH ===
                    double finalAcademicGrade = 100.0;

                    if (SelectedTermView == "Semester Average")
                    {
                        double midterm = CalculateAcademicGrade(row, "Midterm");
                        double final = CalculateAcademicGrade(row, "Final");

                        row.MidtermGradeDisplay = $"{System.Math.Round(midterm, 2)}%";
                        row.FinalTermGradeDisplay = $"{System.Math.Round(final, 2)}%";
                        
                        // Safety Check: Prevent 0% averages if the teacher hasn't created a Final yet!
                        bool hasMidterm = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Midterm");
                        bool hasFinal = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Final");
                        
                        if (hasMidterm && hasFinal) finalAcademicGrade = (midterm + final) / 2.0;
                        else if (hasMidterm) finalAcademicGrade = midterm;
                        else if (hasFinal) finalAcademicGrade = final;
                    }
                    else
                    {
                        finalAcademicGrade = CalculateAcademicGrade(row, SelectedTermView);
                    }

                    // === 2. ATTENDANCE PENALTIES ===
                    var attendanceRow = AttendanceGridRows.FirstOrDefault(a => a.StudentInfo.StudentID == row.StudentID);
                    int totalDays = attendanceRow?.Cells.Count ?? 0;
                    double effectiveAbsences = (attendanceRow?.TotalA ?? 0) + ((attendanceRow?.TotalL ?? 0) * LateValue);

                    string finalOutput = "";
                    double finalNumeric = 0;
                    switch (AttendanceCalculationMode)
                    {
                        case "None":
                            finalOutput = $"{System.Math.Round(finalAcademicGrade, 2)}%";
                            finalNumeric = finalAcademicGrade;
                            break;

                        case "Threshold":
                            if (effectiveAbsences >= MaxAbsencesAllowed)
                            {
                                finalOutput = "FA";
                                finalNumeric = -1; 
                            }
                            else
                            {
                                finalOutput = $"{System.Math.Round(finalAcademicGrade, 2)}%";
                                finalNumeric = finalAcademicGrade;
                            }
                            break;

                        case "Weighted":
                            double attendanceScore = 100.0;
                            if (totalDays > 0)
                            {
                                attendanceScore = ((totalDays - effectiveAbsences) / totalDays) * 100.0;
                                if (attendanceScore < 0) attendanceScore = 0;
                            }

                            double academicWeight = (100.0 - AttendanceWeight) / 100.0;
                            double attWeight = AttendanceWeight / 100.0;
                            
                            double weightedFinal = (finalAcademicGrade * academicWeight) + (attendanceScore * attWeight);
                            
                            finalOutput = $"{System.Math.Round(weightedFinal, 2)}%";
                            finalNumeric = weightedFinal;
                            break;

                        case "Bonus":
                            double bonusFinal = finalAcademicGrade;
                            if (effectiveAbsences == 0 && totalDays > 0) 
                                bonusFinal += AttendanceWeight; 
                            else if (effectiveAbsences > MaxAbsencesAllowed)
                                bonusFinal -= AttendanceWeight; 
                            
                            if (bonusFinal > 100) bonusFinal = 100;
                            if (bonusFinal < 0) bonusFinal = 0;

                            finalOutput = $"{System.Math.Round(bonusFinal, 2)}%";
                            finalNumeric = bonusFinal;
                            break;
                    }

                    // 4. Lock it into the UI!
                    row.FinalGrade = finalOutput;
                    row.FinalGradeNumeric = finalNumeric;
                }
            }            
            private async void ToggleEnrollment()
            {
                IsEnrolling = !IsEnrolling;
                
                if (IsEnrolling)
                {
                    var db = new DatabaseService().GetConnection();
                    var allStudents = await db.Table<Student>().ToListAsync();
                    var enrolledIds = GradebookRows.Select(s => s.StudentID).ToList();

                    AvailableStudents.Clear();
                    foreach (var s in allStudents)
                    {
                        // Only show students who are NOT already enrolled in this class
                        if (s.StudentID != null && !enrolledIds.Contains(s.StudentID))
                        {
                            AvailableStudents.Add(new EnrollmentItemViewModel(s));
                        }
                    }
                }
            }
            private async void SaveEnrollment()
            {
                var db = new DatabaseService().GetConnection();
                
                var selectedStudents = AvailableStudents.Where(s => s.IsSelected).ToList();

                foreach (var student in selectedStudents)
                {
                    var newRosterEntry = new ClassRoster
                    {
                        ClassID = ClassId, // The class we are currently viewing
                        StudentID = student.DbModel.StudentID // The student we just checked
                    };
                    await db.InsertAsync(newRosterEntry); // Link them in Table 3!
                }

                IsEnrolling = false;
                await LoadGradebookData(); // Refresh the grid
                await LoadAttendanceData();
            }
            private async void RemoveStudent(Student student)
            {
                if (student == null) return;
                var db = new DatabaseService().GetConnection();
                
                // Find the exact link in Table 3 and sever it
                var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == student.StudentID).FirstOrDefaultAsync();
                if (rosterEntry != null)
                {
                    await db.DeleteAsync(rosterEntry);
                    await LoadGradebookData();
                    await LoadAttendanceData();
                }
            }

        #endregion

        #region Assessment Module
            [ObservableProperty] public partial bool IsAddingAssessment { get; set; } = false;
            [ObservableProperty] public partial string NewAssessmentTitle { get; set; } = string.Empty;
            [ObservableProperty] public partial double NewAssessmentMaxScore { get; set; } = 100;
            [ObservableProperty] public partial System.DateTime? NewAssessmentDate { get; set; } = System.DateTime.Now;
            private int? _editingAssessmentId = null;
            public IRelayCommand ToggleAddAssessmentCommand { get; }
            public IRelayCommand SaveAssessmentCommand { get; }
            public IRelayCommand<Assessment> EditAssessmentCommand { get; }
            public IRelayCommand<Assessment> DeleteAssessmentCommand { get; }

            private void EditAssessment(Assessment assessment)
            {
                if (assessment == null) return;
                _editingAssessmentId = assessment.AssessmentID;
                NewAssessmentTitle = assessment.Title ?? string.Empty;
                NewAssessmentMaxScore = assessment.MaxScore;
                NewAssessmentDate = assessment.DateGiven;
                SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Name == assessment.Category);
                NewAssessmentPeriod = assessment.GradingPeriod ?? "Midterm"; // <== ADD THIS
                IsAddingAssessment = true; 
            }
            private async void SaveAssessment()
            {
                if (string.IsNullOrWhiteSpace(NewAssessmentTitle) || SelectedCategory == null || NewAssessmentMaxScore <= 0) 
                    return;

                var db = new DatabaseService().GetConnection();

                if (_editingAssessmentId.HasValue)
                {
                    // === UPDATE MODE ===
                    var assessmentToUpdate = await db.Table<Assessment>().Where(a => a.AssessmentID == _editingAssessmentId.Value).FirstOrDefaultAsync();
                    assessmentToUpdate.Title = NewAssessmentTitle;
                    assessmentToUpdate.Category = SelectedCategory.Name;
                    
                    // === FIX: Tell SQLite which term this belongs to! ===
                    assessmentToUpdate.GradingPeriod = NewAssessmentPeriod; 
                    // ====================================================

                    assessmentToUpdate.MaxScore = NewAssessmentMaxScore;
                    assessmentToUpdate.DateGiven = NewAssessmentDate ?? System.DateTime.Now;
                    
                    await db.UpdateAsync(assessmentToUpdate);
                }
                else
                {
                    // === CREATE MODE ===
                    var newAssessment = new Assessment
                    {
                        ClassID = ClassId,
                        Title = NewAssessmentTitle,
                        Category = SelectedCategory.Name,
                        
                        // === FIX: Save the dropdown selection to SQLite! ===
                        GradingPeriod = NewAssessmentPeriod,
                        // ===================================================

                        MaxScore = NewAssessmentMaxScore,
                        DateGiven = NewAssessmentDate ?? System.DateTime.Now
                    };
                    await db.InsertAsync(newAssessment);
                }
                ResetAssessmentForm();
                await LoadGradebookData(); // Refresh the grid!
            }
            private async void DeleteAssessment(Assessment assessment)
            {
                if (assessment == null) return;
                var db = new DatabaseService().GetConnection();
                
                // 1. Delete the Assessment column (Table 4)
                await db.DeleteAsync(assessment);

                // 2. Wipe all the student scores associated with this exact Quiz (Table 5)
                var scoresToDelete = await db.Table<Score>().Where(s => s.AssessmentID == assessment.AssessmentID).ToListAsync();
                foreach (var score in scoresToDelete)
                {
                    await db.DeleteAsync(score);
                }

                await LoadGradebookData(); // Refresh the grid
            }
            private void ResetAssessmentForm()
            {
                _editingAssessmentId = null; // Clears the "Edit Mode" tracking ID
                NewAssessmentTitle = string.Empty;
                NewAssessmentMaxScore = 100;
                NewAssessmentDate = System.DateTime.Now;
                SelectedCategory = null;
                NewAssessmentPeriod = SelectedTermView == "Semester Average" ? "Midterm" : SelectedTermView;
                IsAddingAssessment = false; // Hides the form
            }

        #endregion

        #region Attendance Module
            [ObservableProperty] public partial bool IsAddingRollCall { get; set; } = false;
            [ObservableProperty] public partial System.DateTime? NewRollCallDate { get; set; } = System.DateTime.Today;
            private System.DateTime? _editingRollCallDate = null;
            [ObservableProperty] public partial ObservableCollection<string> AvailableMonths { get; set; } = new();
            [ObservableProperty] public partial string SelectedMonthFilter { get; set; } = "All Months";
            partial void OnSelectedMonthFilterChanged(string value) { TriggerGridRedraw(); }
            [ObservableProperty] public partial bool ShowTotalP { get; set; } = true;
            [ObservableProperty] public partial bool ShowTotalL { get; set; } = true;
            [ObservableProperty] public partial bool ShowTotalA { get; set; } = true;
            partial void OnShowTotalPChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
            partial void OnShowTotalLChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
            partial void OnShowTotalAChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
            public IRelayCommand ToggleAddRollCallCommand { get; }
            public IRelayCommand SaveRollCallCommand { get; }
            public IRelayCommand<System.DateTime?> EditRollCallCommand { get;}
            public IRelayCommand<System.DateTime?> DeleteRollCallCommand { get;} 
            private async void SaveRollCallDay()
            {
                if (!NewRollCallDate.HasValue) return;
                var targetDate = NewRollCallDate.Value.Date;
                var db = new DatabaseService().GetConnection();

                if (_editingRollCallDate.HasValue)
                {
                    // === UPDATE MODE ===
                    var oldDate = _editingRollCallDate.Value;
                    
                    // If they changed the date, check if the new date already exists!
                    if (oldDate != targetDate && AttendanceDates.Contains(targetDate))
                    {
                        ShowToastMessage?.Invoke("Roll call for this date already exists!");
                        return;
                    }

                    // Update all records that belonged to the old date
                    var recordsToUpdate = await db.Table<AttendanceRecord>().Where(a => a.ClassID == ClassId && a.Date == oldDate).ToListAsync();
                    foreach (var r in recordsToUpdate)
                    {
                        r.Date = targetDate;
                        await db.UpdateAsync(r);
                    }
                }
                else
                {
                    // === CREATE MODE ===
                    if (AttendanceDates.Contains(targetDate))
                    {
                        ShowToastMessage?.Invoke("Roll call for this date already exists!");
                        return;
                    }

                    await db.InsertAsync(new AttendanceRecord { ClassID = ClassId, StudentID = "GHOST_DATE", Date = targetDate, Status = "GHOST" });

                    var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
                    foreach (var r in roster)
                    {
                        await db.InsertAsync(new AttendanceRecord { ClassID = ClassId, StudentID = r.StudentID, Date = targetDate, Status = "P" });
                    }
                }

                ResetRollCallForm();
                await LoadAttendanceData(); // Refresh the grid!
            } 
            private void EditRollCall(System.DateTime? dateParam)
            {
                if (!dateParam.HasValue) return;
                _editingRollCallDate = dateParam.Value.Date;
                NewRollCallDate = dateParam.Value.Date;
                IsAddingRollCall = true; // Slide the panel open!
            }
            private async void DeleteRollCall(System.DateTime? dateParam)
            {
                if (!dateParam.HasValue) return;
                var targetDate = dateParam.Value.Date;
                var db = new DatabaseService().GetConnection();
                
                // Delete ALL records for this class on this specific date
                var recordsToDelete = await db.Table<AttendanceRecord>().Where(a => a.ClassID == ClassId && a.Date == targetDate).ToListAsync();
                foreach (var r in recordsToDelete)
                {
                    await db.DeleteAsync(r);
                }
                
                await LoadAttendanceData(); // Refresh the grid!
            }
            private void ResetRollCallForm()
            {
                _editingRollCallDate = null;
                NewRollCallDate = System.DateTime.Today;
                IsAddingRollCall = false;
            }

        #endregion
    }

    public partial class EnrollmentItemViewModel : ObservableObject
    {
        public Student DbModel { get; }
        
        [ObservableProperty] public partial bool IsSelected { get; set; } = false;

        public string FullName => $"{DbModel.LastName}, {DbModel.FirstName}";
        public string StudentID => DbModel.StudentID ?? "";

        public EnrollmentItemViewModel(Student student)
        {
            DbModel = student;
        }
    }
    public partial class StudentGradeRow(Student student) : ObservableObject
    {
        public Student StudentInfo { get; } = student;
        public System.Collections.Generic.Dictionary<int, ScoreCellViewModel> Scores { get; set; } = [];
        public string FullName => $"{StudentInfo.LastName}, {StudentInfo.FirstName}";
        public string StudentID => StudentInfo.StudentID ?? "";
        [ObservableProperty] public partial string MidtermGradeDisplay { get; set; } = "---";
        [ObservableProperty] public partial string FinalTermGradeDisplay { get; set; } = "---";
        [ObservableProperty] public partial string FinalGrade { get; set; } = "---";
        [ObservableProperty] public partial double FinalGradeNumeric { get; set; } = 0;
    }
    public partial class ScoreCellViewModel : ObservableObject
    {
        public Score DbModel { get; }
        public double MaxScore { get; }
        private readonly System.Action _onScoreChanged; 

        public double PointsEarned
        {
            get => DbModel.PointsEarned;
            set
            {
                double finalValue = value;
                if (finalValue > MaxScore) finalValue = MaxScore; 
                if (finalValue < 0) finalValue = 0;               

                if (DbModel.PointsEarned != finalValue)
                {
                    DbModel.PointsEarned = finalValue;
                    OnPropertyChanged(); 
                    SaveScoreToDatabase(); 
                    _onScoreChanged?.Invoke(); 
                }
            }
        }

        public ScoreCellViewModel(Score score, double maxScore, System.Action onScoreChanged)
        {
            DbModel = score;
            MaxScore = maxScore;
            _onScoreChanged = onScoreChanged;
        }

        private async void SaveScoreToDatabase()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            if (DbModel.ScoreID == 0) await db.InsertAsync(DbModel);
            else await db.UpdateAsync(DbModel);
        }
    }
    public partial class AssessmentFilterViewModel : ObservableObject
    {
        public Assessment DbModel { get; }
        private readonly System.Action _onVisibilityChanged;

        public string Title => DbModel.Title ?? "Unknown";

        public bool IsVisible
        {
            get => DbModel.IsVisible;
            set
            {
                // If the teacher clicks the checkbox, save it to SQLite instantly and redraw the grid!
                if (DbModel.IsVisible != value)
                {
                    DbModel.IsVisible = value;
                    OnPropertyChanged();
                    SaveToDb();
                    _onVisibilityChanged?.Invoke(); 
                }
            }
        }

        public AssessmentFilterViewModel(Assessment assessment, System.Action onVisibilityChanged)
        {
            DbModel = assessment;
            _onVisibilityChanged = onVisibilityChanged;
        }

        private async void SaveToDb()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.UpdateAsync(DbModel);
        }
    }
    public partial class CategoryFilterViewModel : ObservableObject
    {
        public string CategoryName { get; }
        public ObservableCollection<AssessmentFilterViewModel> Assessments { get; }

        // The Master Checkbox: If checked, it loops through and checks all children!
        public bool IsCategoryVisible
        {
            get => Assessments.Any(a => a.IsVisible); 
            set
            {
                foreach (var a in Assessments)
                {
                    a.IsVisible = value; 
                }
                OnPropertyChanged();
            }
        }

        public CategoryFilterViewModel(string name, System.Collections.Generic.IEnumerable<AssessmentFilterViewModel> assessments)
        {
            CategoryName = name;
            Assessments = new ObservableCollection<AssessmentFilterViewModel>(assessments);
            
            // Listen to children: If all quizzes are hidden, uncheck the master Category checkbox automatically
            foreach(var a in Assessments) 
            {
                a.PropertyChanged += (s,e) => 
                {
                    if (e.PropertyName == nameof(AssessmentFilterViewModel.IsVisible))
                        OnPropertyChanged(nameof(IsCategoryVisible));
                };
            }
        }
    }
    public partial class AttendanceCellViewModel : ObservableObject
    {
        public AttendanceRecord DbModel { get; }
        private readonly System.Action _onStatusChanged;
        private readonly System.Action<string> _showToast;

        public string Status
        {
            get => DbModel.Status ?? "";
            set
            {
                // 1. Instantly force to Uppercase
                string input = value?.ToUpper() ?? ""; 
                
                // 2. Validate input!
                if (input == "P" || input == "L" || input == "A" || input == "")
                {
                    if (DbModel.Status != input)
                    {
                        DbModel.Status = input;
                        OnPropertyChanged();
                        SaveToDb();
                        _onStatusChanged?.Invoke(); // Recalculate totals instantly!
                    }
                }
                else
                {
                    // Invalid! Revert the UI to what it was and fire the Toast!
                    OnPropertyChanged(); 
                    _showToast?.Invoke($"'{input}' is invalid. Only use P, L, or A.");
                }
            }
        }

        public AttendanceCellViewModel(AttendanceRecord record, System.Action onStatusChanged, System.Action<string> showToast)
        {
            DbModel = record;
            _onStatusChanged = onStatusChanged;
            _showToast = showToast;
        }

        private async void SaveToDb()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            if (DbModel.RecordID == 0) await db.InsertAsync(DbModel);
            else await db.UpdateAsync(DbModel);
        }
    }
    public partial class AttendanceGridRowViewModel : ObservableObject
    {
        public Student StudentInfo { get; }
        public string LastName => StudentInfo.LastName ?? "";
        public string FirstName => StudentInfo.FirstName ?? "";

        // Dictionary to link a specific Date Column to its specific Cell
        public System.Collections.Generic.Dictionary<string, AttendanceCellViewModel> Cells { get; set; } = [];

        public int TotalP => Cells.Values.Count(c => c.Status == "P");
        public int TotalL => Cells.Values.Count(c => c.Status == "L");
        public int TotalA => Cells.Values.Count(c => c.Status == "A");

        public void RefreshTotals()
        {
            OnPropertyChanged(nameof(TotalP));
            OnPropertyChanged(nameof(TotalL));
            OnPropertyChanged(nameof(TotalA));
        }

        public AttendanceGridRowViewModel(Student student)
        {
            StudentInfo = student;
        }
    }
}