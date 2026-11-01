using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Centriku.Services;
using Centriku.Models;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Centriku.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly IDatabaseService _dbService;
        private readonly System.Action<ViewModelBase> _navigateAction;
        private bool _isUpdatingFilters = false;

        [ObservableProperty] public partial ObservableCollection<AttentionAlertViewModel> AttentionAlerts { get; set; } = new();
        private List<Student> _allStudents = [];

        // KPI PROPERTIES 
        [ObservableProperty] public partial int TotalStudents { get; set; } = 0;
        [ObservableProperty] public partial int TotalSystemStudents { get; set; } = 0;
        [ObservableProperty] public partial int ActiveClasses { get; set; } = 0;
        [ObservableProperty] public partial int InactiveClasses { get; set; } = 0;
        [ObservableProperty] public partial int NeedsAttention { get; set; } = 0;
        [ObservableProperty] public partial int CriticalFailuresCount { get; set; } = 0;
        [ObservableProperty] public partial ObservableCollection<AttentionAlertViewModel> CriticalAlerts { get; set; } = new();
        public bool HasNoCriticalAlerts { get; set; } = true;
        partial void OnNeedsAttentionChanged(int value) => OnPropertyChanged(nameof(HasNoAlerts));
        public bool HasNoAlerts => NeedsAttention == 0;

        [ObservableProperty] public partial ObservableCollection<DashboardClassCardViewModel> ClassCards { get; set; } = new();
        public bool HasNoClasses => ClassCards == null || ClassCards.Count == 0;
        partial void OnClassCardsChanged(ObservableCollection<DashboardClassCardViewModel> value) => OnPropertyChanged(nameof(HasNoClasses));

        // GLOBAL FILTER PROPERTIES (Pre-seeded so ComboBoxes never render blank on startup)
        [ObservableProperty] public partial ObservableCollection<string> AvailableYears { get; set; } = ["All Years"];
        [ObservableProperty] public partial string SelectedYear { get; set; } = "All Years";
        partial void OnSelectedYearChanged(string value)
        {
            if (!_isUpdatingFilters) _ = CalculateDashboardMetricsAsync();
        }

        [ObservableProperty] public partial ObservableCollection<string> AvailableTerms { get; set; } = ["All Terms"];
        [ObservableProperty] public partial string SelectedTerm { get; set; } = "All Terms";
        partial void OnSelectedTermChanged(string value)
        {
            if (!_isUpdatingFilters) _ = CalculateDashboardMetricsAsync();
        }

        [ObservableProperty] public partial ObservableCollection<string> AvailablePrograms { get; set; } = ["All Programs"];
        [ObservableProperty] public partial string SelectedProgram { get; set; } = "All Programs";
        partial void OnSelectedProgramChanged(string value)
        {
            if (!_isUpdatingFilters) _ = CalculateDashboardMetricsAsync();
        }

        // IN-MEMORY CACHE
        private List<TeacherClass> _allClasses = [];
        private List<ClassRoster> _allRosters = [];
        private List<GradingTemplate> _allTemplates = [];
        private List<Assessment> _allAssessments = [];
        private List<Score> _allScores = [];
        private List<AttendanceRecord> _allAttendance = [];
        private List<GradingCategory> _allCategories = [];

        public IRelayCommand<DashboardClassCardViewModel> OpenGradebookCommand { get; }
        public IRelayCommand<DashboardClassCardViewModel> OpenAttendanceCommand { get; }
        public IRelayCommand<AttentionAlertViewModel> OpenStudentAlertCommand { get; } 

        public DashboardViewModel(IDatabaseService dbService, System.Action<ViewModelBase> navigateAction)
        {
            _dbService = dbService;
            _navigateAction = navigateAction;
            OpenGradebookCommand = new RelayCommand<DashboardClassCardViewModel>(OpenGradebook!);
            OpenAttendanceCommand = new RelayCommand<DashboardClassCardViewModel>(OpenAttendance!);
            OpenStudentAlertCommand = new RelayCommand<AttentionAlertViewModel>(OpenStudentAlert!); 
        }

        public async Task LoadDashboardDataAsync()
        {
            var db = _dbService.GetConnection();
            _allClasses = await db.Table<TeacherClass>().ToListAsync();
            
            var rawStudents = await db.Table<Student>().ToListAsync();
            _allStudents = rawStudents.Where(s => !s.IsArchived && s.EnrollmentStatus != "Dropped").ToList();
            var activeStudentIds = _allStudents.Select(s => s.StudentID).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();

            var rawRosters = await db.Table<ClassRoster>().ToListAsync();
            _allRosters = activeStudentIds.Count > 0
                ? rawRosters.Where(r => r.StudentID != null && activeStudentIds.Contains(r.StudentID)).ToList()
                : rawRosters;

            _allTemplates = await db.Table<GradingTemplate>().ToListAsync();
            _allAssessments = await db.Table<Assessment>().ToListAsync();
            _allScores = await db.Table<Score>().ToListAsync();
            _allAttendance = await db.Table<AttendanceRecord>().ToListAsync();
            _allCategories = await db.Table<GradingCategory>().ToListAsync();

            var activeDbClasses = _allClasses.Where(c => c.IsActive).ToList();
            
            _isUpdatingFilters = true;
            try
            {
                // 1. YEAR DROPDOWN
                var years = activeDbClasses.Select(c => c.AcademicYear).Where(y => !string.IsNullOrWhiteSpace(y)).Distinct().OrderByDescending(y => y).ToList();
                var prevYear = string.IsNullOrWhiteSpace(SelectedYear) ? "All Years" : SelectedYear;
                AvailableYears.Clear(); 
                AvailableYears.Add("All Years");
                foreach (var y in years) AvailableYears.Add(y!);
                string targetYear = AvailableYears.Contains(prevYear) ? prevYear : "All Years";
                SelectedYear = string.Empty;
                SelectedYear = targetYear;

                // 2. TERM DROPDOWN
                var terms = activeDbClasses.Select(c => c.Term).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().OrderBy(t => t).ToList();
                var prevTerm = string.IsNullOrWhiteSpace(SelectedTerm) ? "All Terms" : SelectedTerm;
                AvailableTerms.Clear(); 
                AvailableTerms.Add("All Terms");
                foreach (var t in terms) AvailableTerms.Add(t!);
                string targetTerm = AvailableTerms.Contains(prevTerm) ? prevTerm : "All Terms";
                SelectedTerm = string.Empty;
                SelectedTerm = targetTerm;

                // 3. PROGRAM DROPDOWN
                var programs = activeDbClasses.Select(c => c.Program).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().OrderBy(p => p).ToList();
                var prevProgram = string.IsNullOrWhiteSpace(SelectedProgram) ? "All Programs" : SelectedProgram;
                AvailablePrograms.Clear(); 
                AvailablePrograms.Add("All Programs");
                foreach (var p in programs) AvailablePrograms.Add(p!);
                string targetProgram = AvailablePrograms.Contains(prevProgram) ? prevProgram : "All Programs";
                SelectedProgram = string.Empty;
                SelectedProgram = targetProgram;
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            await CalculateDashboardMetricsAsync();
        }

        private Task CalculateDashboardMetricsAsync()
        {
            // 1. FILTER CLASSES (Synchronous in-memory evaluation prevents async race conditions)
            var filteredClasses = _allClasses.Where(c => c.IsActive).AsEnumerable();
            if (!string.IsNullOrEmpty(SelectedYear) && SelectedYear != "All Years") filteredClasses = filteredClasses.Where(c => c.AcademicYear == SelectedYear);
            if (!string.IsNullOrEmpty(SelectedTerm) && SelectedTerm != "All Terms") filteredClasses = filteredClasses.Where(c => c.Term == SelectedTerm);
            if (!string.IsNullOrEmpty(SelectedProgram) && SelectedProgram != "All Programs") filteredClasses = filteredClasses.Where(c => c.Program == SelectedProgram);

            var activeClassList = filteredClasses.ToList();
            var activeClassIds = activeClassList.Select(c => c.ClassID).ToHashSet();

            // 2. CALCULATE METRICS
            ActiveClasses = activeClassList.Count;
            
            var inactiveList = _allClasses.Where(c => !c.IsActive).AsEnumerable();
            if (!string.IsNullOrEmpty(SelectedYear) && SelectedYear != "All Years") inactiveList = inactiveList.Where(c => c.AcademicYear == SelectedYear);
            if (!string.IsNullOrEmpty(SelectedTerm) && SelectedTerm != "All Terms") inactiveList = inactiveList.Where(c => c.Term == SelectedTerm);
            if (!string.IsNullOrEmpty(SelectedProgram) && SelectedProgram != "All Programs") inactiveList = inactiveList.Where(c => c.Program == SelectedProgram);
            InactiveClasses = inactiveList.Count();

            var activeRosters = _allRosters.Where(r => activeClassIds.Contains(r.ClassID)).ToList();
            TotalStudents = activeRosters.Select(r => r.StudentID).Distinct().Count();
            TotalSystemStudents = _allStudents.Count;

            // 3. COMBINED ENGINE: Calculate Alerts & Build Cards
            var newCards = new ObservableCollection<DashboardClassCardViewModel>();
            var newAlerts = new List<AttentionAlertViewModel>();
            var newCriticalAlerts = new List<AttentionAlertViewModel>();
            int riskCounter = 0;
            int failedCounter = 0;
            
            foreach (var teacherClass in activeClassList)
            {
                var template = _allTemplates.FirstOrDefault(t => t.TemplateID == teacherClass.GradingTemplateID);
                var classCategories = _allCategories.Where(c => c.TemplateID == teacherClass.GradingTemplateID).ToList();

                var allClassAssessments = _allAssessments.Where(a => a.ClassID == teacherClass.ClassID).ToList();
                var allClassAssessmentIds = allClassAssessments.Select(a => a.AssessmentID).ToHashSet();
                var classScores = _allScores.Where(s => allClassAssessmentIds.Contains(s.AssessmentID)).ToList();
                var classAttendance = _allAttendance.Where(a => a.ClassID == teacherClass.ClassID).ToList();
                
                var classStudents = activeRosters.Where(r => r.ClassID == teacherClass.ClassID).Select(r => r.StudentID).ToList();

                int totalCount = classStudents.Count;
                int passedCount = 0;
                int atRiskCount = 0; 
                int failedCount = 0;

                foreach (var studentId in classStudents)
                {
                    if (string.IsNullOrEmpty(studentId)) continue;
                    var studentScores = classScores.Where(s => s.StudentID == studentId).ToList();
                    var studentAtt = classAttendance.Where(a => a.StudentID == studentId).ToList();

                    bool isFdaCritical = false; 
                    bool isFdaAtRisk = false; 

                    // --- 1. FDA CHECK (40% Proximity Rule — Supports Equivalent & Independent Directly) ---
                    if (teacherClass.IsFdaEnabled)
                    {
                        double totalA = studentAtt.Where(a => a.Status == "A").Sum(a => a.SessionHours);
                        double totalL = studentAtt.Where(a => a.Status == "L").Sum(a => a.SessionHours);
                        
                        if (teacherClass.FdaRuleType == "Independent")
                        {
                            if (totalA >= teacherClass.FdaMaxAbsenceHours || totalL >= teacherClass.FdaMaxLateHours)
                            {
                                isFdaCritical = true;
                            }
                            else
                            {
                                bool nearAbsenceLimit = (teacherClass.FdaMaxAbsenceHours - totalA) <= (teacherClass.FdaMaxAbsenceHours * 0.40);
                                bool nearLateLimit = teacherClass.FdaMaxLateHours > 0 && (teacherClass.FdaMaxLateHours - totalL) <= (teacherClass.FdaMaxLateHours * 0.40);
                                if (nearAbsenceLimit || nearLateLimit) isFdaAtRisk = true;
                            }
                        }
                        else // "Equivalent"
                        {
                            double equivalentAbsences = totalA + (totalL * teacherClass.FdaLatePenaltyRatio);
                            if (equivalentAbsences >= teacherClass.FdaMaxAbsenceHours)
                            {
                                isFdaCritical = true;
                            }
                            else if ((teacherClass.FdaMaxAbsenceHours - equivalentAbsences) <= (teacherClass.FdaMaxAbsenceHours * 0.40))
                            {
                                isFdaAtRisk = true;
                            }
                        }
                    }

                    // --- 2. ACADEMIC DATA INTEGRITY CHECK ---
                    bool isMidtermComplete = false;
                    bool isFinalComplete = false;
                    double midtermGrade = 0;
                    double finalGrade = 0;

                    var midAssessments = allClassAssessments.Where(a => string.Equals((a.GradingPeriod ?? "").Trim(), "Midterm", StringComparison.OrdinalIgnoreCase)).ToList();
                    var finAssessments = allClassAssessments.Where(a => string.Equals((a.GradingPeriod ?? "").Trim(), "Final", StringComparison.OrdinalIgnoreCase)).ToList();

                    if (midAssessments.Any() && classCategories.All(c => midAssessments.Any(a => string.Equals((a.Category ?? "").Trim(), (c.Name ?? "").Trim(), StringComparison.OrdinalIgnoreCase))) && 
                        midAssessments.All(a => studentScores.Any(s => s.AssessmentID == a.AssessmentID)))
                    {
                        isMidtermComplete = true;
                        midtermGrade = GradeCalculationService.EvaluateFinalGrade(classCategories, midAssessments, studentScores, template ?? new GradingTemplate(), teacherClass, studentAtt).TermNumericGrade;
                    }

                    if (finAssessments.Any() && classCategories.All(c => finAssessments.Any(a => string.Equals((a.Category ?? "").Trim(), (c.Name ?? "").Trim(), StringComparison.OrdinalIgnoreCase))) && 
                        finAssessments.All(a => studentScores.Any(s => s.AssessmentID == a.AssessmentID)))
                    {
                        isFinalComplete = true;
                        finalGrade = GradeCalculationService.EvaluateFinalGrade(classCategories, finAssessments, studentScores, template ?? new GradingTemplate(), teacherClass, studentAtt).TermNumericGrade;
                    }

                    // --- 3. BUCKET ASSIGNMENT LOGIC ---
                    bool isFailed = isFdaCritical;
                    bool isAtRisk = isFdaAtRisk;
                    bool isPassed = false;
                    string failureReason = isFdaCritical ? "Dropped: Exceeded Max Absences (FDA)" : "";
                    string warningReason = isFdaAtRisk ? "Warning: Approaching Absence Limit" : "";

                    if (isMidtermComplete && isFinalComplete)
                    {
                        double semAvg = Math.Round((midtermGrade + finalGrade) / 2.0, 0, MidpointRounding.AwayFromZero);
                        if (semAvg < (template?.PassingGrade ?? 75.0))
                        {
                            isFailed = true;
                            isAtRisk = false;
                            failureReason = "Failed: Semester Average Below Passing";
                        }
                        else if (!isFdaCritical)
                        {
                            isPassed = true;
                        }
                    }
                    else if (isMidtermComplete && !isFinalComplete)
                    {
                        if (midtermGrade < (template?.PassingGrade ?? 75.0))
                        {
                            isAtRisk = true;
                            warningReason = "Warning: Midterm Average Below Passing";
                        }
                    }

                    // --- 4. TALLY & CREATE UI CARDS ---
                    var student = _allStudents.FirstOrDefault(s => s.StudentID == studentId);
                    string studentName = student != null ? $"{student.LastName}, {student.FirstName}" : studentId;
                    string classString = $"{teacherClass.SubjectName} ({teacherClass.Program}_{teacherClass.SectionLabel})";

                    if (isFailed)
                    {
                        failedCount++;
                        failedCounter++;
                        newCriticalAlerts.Add(new AttentionAlertViewModel
                        {
                            ClassId = teacherClass.ClassID,
                            SubjectName = teacherClass.SubjectName ?? string.Empty,
                            StudentId = studentId,
                            StudentName = studentName,
                            ClassName = classString,
                            AbsencesText = failureReason,
                            IsCritical = true
                        });
                    }
                    else if (isAtRisk)
                    {
                        atRiskCount++;
                        riskCounter++;
                        newAlerts.Add(new AttentionAlertViewModel
                        {
                            ClassId = teacherClass.ClassID,
                            SubjectName = teacherClass.SubjectName ?? string.Empty,
                            StudentId = studentId,
                            StudentName = studentName,
                            ClassName = classString,
                            AbsencesText = warningReason,
                            IsCritical = false
                        });
                    }
                    else if (isPassed)
                    {
                        passedCount++; 
                    }
                }

                newCards.Add(new DashboardClassCardViewModel(teacherClass, totalCount, passedCount, atRiskCount, failedCount));
            }
            
            NeedsAttention = riskCounter;
            CriticalFailuresCount = failedCounter;
            ClassCards = newCards; 
            AttentionAlerts = new ObservableCollection<AttentionAlertViewModel>(newAlerts);
            
            CriticalAlerts = new ObservableCollection<AttentionAlertViewModel>(newCriticalAlerts); 
            HasNoCriticalAlerts = CriticalAlerts.Count == 0;
            OnPropertyChanged(nameof(CriticalAlerts));
            OnPropertyChanged(nameof(HasNoCriticalAlerts));

            return Task.CompletedTask;
        }

        private void OpenGradebook(DashboardClassCardViewModel selectedClass)
        {
            if (selectedClass != null)
            {
                var gradebookVM = new GradebookViewModel(_dbService);
                gradebookVM.Initialize(selectedClass.ClassRecord.ClassID, selectedClass.ClassRecord.SubjectName ?? string.Empty, 0); 
                _navigateAction(gradebookVM);
            }
        }

        private void OpenAttendance(DashboardClassCardViewModel selectedClass)
        {
            if (selectedClass != null)
            {
                var gradebookVM = new GradebookViewModel(_dbService);
                gradebookVM.Initialize(selectedClass.ClassRecord.ClassID, selectedClass.ClassRecord.SubjectName ?? string.Empty, 2); 
                _navigateAction(gradebookVM);
            }
        }

        private void OpenStudentAlert(AttentionAlertViewModel alert)
        {
            if (alert != null)
            {
                string title = !string.IsNullOrWhiteSpace(alert.SubjectName) ? alert.SubjectName : alert.ClassName;
                var gradebookVM = new GradebookViewModel(_dbService);
                gradebookVM.Initialize(alert.ClassId, title, 0, alert.StudentId);
                _navigateAction(gradebookVM);
            }
        }
    }

    public partial class DashboardClassCardViewModel(TeacherClass teacherClass, int total, int passed, int atRisk, int failed) : ObservableObject
    {
        public TeacherClass ClassRecord { get; } = teacherClass;
        
        public int TotalCount { get; } = total;
        public int PassedCount { get; } = passed;
        public int AtRiskCount { get; } = atRisk;
        public int FailedCount { get; } = failed;
        
        public bool HasPassed => PassedCount > 0;
        public bool HasAtRisk => AtRiskCount > 0; 
        public bool HasFailed => FailedCount > 0; 
        
        public string FullTitle => ClassRecord.SubjectName ?? "Untitled Subject";
        public string ProgramSectionDisplay => $"{ClassRecord.Program} • {ClassRecord.SectionLabel}";
        public string Subtitle => $"{ClassRecord.Term} | {ClassRecord.AcademicYear}";
    }

    public partial class AttentionAlertViewModel : ObservableObject
    {
        public int ClassId { get; set;}
        public string SubjectName { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty; 
        public string StudentName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string AbsencesText { get; set; } = string.Empty;
        public bool IsCritical { get; set; } = false;
    }
}