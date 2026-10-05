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
        [ObservableProperty] public partial ObservableCollection<AttentionAlertViewModel> AttentionAlerts { get; set; } = new();
        private List<Student> _allStudents = [];

        // KPI PROPERTIES 
        [ObservableProperty] public partial int TotalStudents { get; set; } = 0;
        [ObservableProperty] public partial int TotalSystemStudents { get; set; } = 0;
        [ObservableProperty] public partial int ActiveClasses { get; set; } = 0;
        [ObservableProperty] public partial int InactiveClasses { get; set; } = 0;
        [ObservableProperty] public partial int NeedsAttention { get; set; } = 0;
        [ObservableProperty] public partial ObservableCollection<AttentionAlertViewModel> CriticalAlerts { get; set; } = new();
        public bool HasNoCriticalAlerts { get; set; } = true;
        partial void OnNeedsAttentionChanged(int value) => OnPropertyChanged(nameof(HasNoAlerts));
        public bool HasNoAlerts => NeedsAttention == 0;
        [ObservableProperty] public partial ObservableCollection<DashboardClassCardViewModel> ClassCards { get; set; } = new();

        // GLOBAL FILTER PROPERTIES 
        [ObservableProperty] public partial ObservableCollection<string> AvailableYears { get; set; } = new();
        [ObservableProperty] public partial string SelectedYear { get; set; } = "All Years";
        partial void OnSelectedYearChanged(string value) => _ = CalculateDashboardMetricsAsync();

        [ObservableProperty] public partial ObservableCollection<string> AvailableTerms { get; set; } = new();
        [ObservableProperty] public partial string SelectedTerm { get; set; } = "All Terms";
        partial void OnSelectedTermChanged(string value) => _ = CalculateDashboardMetricsAsync();

        // NEW: PROGRAM FILTER
        [ObservableProperty] public partial ObservableCollection<string> AvailablePrograms { get; set; } = new();
        [ObservableProperty] public partial string SelectedProgram { get; set; } = "All Programs";
        partial void OnSelectedProgramChanged(string value) => _ = CalculateDashboardMetricsAsync();

        // IN-MEMORY CACHE (Prevents database lag!) 
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

        public DashboardViewModel(IDatabaseService dbService,System.Action<ViewModelBase> navigateAction)
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
            _allStudents = await db.Table<Student>().ToListAsync();
            _allRosters = await db.Table<ClassRoster>().ToListAsync();
            _allTemplates = await db.Table<GradingTemplate>().ToListAsync();
            _allAssessments = await db.Table<Assessment>().ToListAsync();
            _allScores = await db.Table<Score>().ToListAsync();
            _allAttendance = await db.Table<AttendanceRecord>().ToListAsync();
            _allCategories = await db.Table<GradingCategory>().ToListAsync();

            var activeDbClasses = _allClasses.Where(c => c.IsActive).ToList();
            
            // 1. YEAR DROPDOWN
            var years = activeDbClasses.Select(c => c.AcademicYear).Where(y => !string.IsNullOrWhiteSpace(y)).Distinct().OrderByDescending(y => y).ToList();
            var prevYear = SelectedYear;
            AvailableYears.Clear(); 
            AvailableYears.Add("All Years");
            foreach (var y in years) AvailableYears.Add(y!);
            SelectedYear = string.Empty; // Force UI Detach
            SelectedYear = AvailableYears.Contains(prevYear) ? prevYear : "All Years"; // Force UI Attach

            // 2. TERM DROPDOWN
            var terms = activeDbClasses.Select(c => c.Term).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().OrderBy(t => t).ToList();
            var prevTerm = SelectedTerm;
            AvailableTerms.Clear(); 
            AvailableTerms.Add("All Terms");
            foreach (var t in terms) AvailableTerms.Add(t!);
            SelectedTerm = string.Empty; 
            SelectedTerm = AvailableTerms.Contains(prevTerm) ? prevTerm : "All Terms";

            // 3. PROGRAM DROPDOWN
            var programs = activeDbClasses.Select(c => c.Program).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().OrderBy(p => p).ToList();
            var prevProgram = SelectedProgram;
            AvailablePrograms.Clear(); 
            AvailablePrograms.Add("All Programs");
            foreach (var p in programs) AvailablePrograms.Add(p!);
            SelectedProgram = string.Empty;
            SelectedProgram = AvailablePrograms.Contains(prevProgram) ? prevProgram : "All Programs";

            await CalculateDashboardMetricsAsync();
        }

        private async Task CalculateDashboardMetricsAsync()
        {
            await Task.Delay(20);

            // 1. FILTER CLASSES
            var filteredClasses = _allClasses.Where(c => c.IsActive).AsEnumerable();
            if (SelectedYear != "All Years") filteredClasses = filteredClasses.Where(c => c.AcademicYear == SelectedYear);
            if (SelectedTerm != "All Terms") filteredClasses = filteredClasses.Where(c => c.Term == SelectedTerm);
            if (SelectedProgram != "All Programs") filteredClasses = filteredClasses.Where(c => c.Program == SelectedProgram);

            var activeClassList = filteredClasses.ToList();
            var activeClassIds = activeClassList.Select(c => c.ClassID).ToList();

            // 2. CALCULATE METRICS
            ActiveClasses = activeClassList.Count;
            
            // Calculate inactive classes (respecting the current Year/Term filters)
            var inactiveList = _allClasses.Where(c => !c.IsActive).AsEnumerable();
            if (SelectedYear != "All Years") inactiveList = inactiveList.Where(c => c.AcademicYear == SelectedYear);
            if (SelectedTerm != "All Terms") inactiveList = inactiveList.Where(c => c.Term == SelectedTerm);
            if (SelectedProgram != "All Programs") inactiveList = inactiveList.Where(c => c.Program == SelectedProgram);
            InactiveClasses = inactiveList.Count();

            // Total Enrolled (Unique students currently taking active classes)
            var activeRosters = _allRosters.Where(r => activeClassIds.Contains(r.ClassID)).ToList();
            TotalStudents = activeRosters.Select(r => r.StudentID).Distinct().Count();
            
            // Total System Students (Everyone in the database directory)
            TotalSystemStudents = _allStudents.Count;

            // 3. COMBINED ENGINE: Calculate Alerts & Build Cards
            var newCards = new ObservableCollection<DashboardClassCardViewModel>();
            var newAlerts = new List<AttentionAlertViewModel>();
            var newCriticalAlerts = new List<AttentionAlertViewModel>(); // NEW: Separate bucket for failures
            int riskCounter = 0;
            int failedCounter = 0;
            
            foreach (var teacherClass in activeClassList)
            {
                var template = _allTemplates.FirstOrDefault(t => t.TemplateID == teacherClass.GradingTemplateID);
                var classCategories = _allCategories.Where(c => c.TemplateID == teacherClass.GradingTemplateID).ToList();

                var allClassAssessments = _allAssessments.Where(a => a.ClassID == teacherClass.ClassID).ToList();
                var allClassAssessmentIds = allClassAssessments.Select(a => a.AssessmentID).ToList();
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

                    // --- 1. FDA CHECK (40% Proximity Rule) ---
                    if (teacherClass.IsFdaEnabled)
                    {
                        double totalA = studentAtt.Where(a => a.Status == "A").Sum(a => a.SessionHours);
                        double totalL = studentAtt.Where(a => a.Status == "L").Sum(a => a.SessionHours);
                        
                        double equivalentAbsences = teacherClass.FdaRuleType == "Equivalent" 
                            ? totalA + (totalL * teacherClass.FdaLatePenaltyRatio) 
                            : Math.Max(totalA, totalL * (teacherClass.FdaMaxAbsenceHours / Math.Max(1, teacherClass.FdaMaxLateHours))); 

                        if (equivalentAbsences >= teacherClass.FdaMaxAbsenceHours) isFdaCritical = true; 
                        else
                        {
                            double hoursRemaining = teacherClass.FdaMaxAbsenceHours - equivalentAbsences;
                            double safeThreshold = teacherClass.FdaMaxAbsenceHours * 0.40;
                            if (hoursRemaining <= safeThreshold) isFdaAtRisk = true;
                        }
                    }

                    // --- 2. ACADEMIC DATA INTEGRITY CHECK ---
                    bool isMidtermComplete = false;
                    bool isFinalComplete = false;
                    double midtermGrade = 0;
                    double finalGrade = 0;

                    var midAssessments = allClassAssessments.Where(a => a.GradingPeriod == "Midterm").ToList();
                    var finAssessments = allClassAssessments.Where(a => a.GradingPeriod == "Final").ToList();

                    // Check Midterm: Do all categories exist AND does the student have saved scores for ALL of them?
                    if (midAssessments.Any() && classCategories.All(c => midAssessments.Any(a => a.Category == c.Name)) && 
                        midAssessments.All(a => studentScores.Any(s => s.AssessmentID == a.AssessmentID)))
                    {
                        isMidtermComplete = true;
                        midtermGrade = GradeCalculationService.EvaluateFinalGrade(classCategories, midAssessments, studentScores, template ?? new GradingTemplate(), teacherClass, studentAtt).TermNumericGrade;
                    }

                    // Check Final: Do all categories exist AND does the student have saved scores for ALL of them?
                    if (finAssessments.Any() && classCategories.All(c => finAssessments.Any(a => a.Category == c.Name)) && 
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
                            isAtRisk = false; // Upgrade warning to critical
                            failureReason = "Failed: Semester Average Below Passing";
                        }
                        else if (!isFdaCritical)
                        {
                            isPassed = true; // Passed successfully!
                        }
                    }
                    else if (isMidtermComplete && !isFinalComplete)
                    {
                        if (midtermGrade < (template?.PassingGrade ?? 75.0))
                        {
                            isAtRisk = true;
                            warningReason = "Warning: Failed Midterm Exam";
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
                        newCriticalAlerts.Add(new AttentionAlertViewModel { ClassId = teacherClass.ClassID, StudentId = studentId, StudentName = studentName, ClassName = classString, AbsencesText = failureReason, IsCritical = true });
                    }
                    else if (isAtRisk)
                    {
                        atRiskCount++;
                        riskCounter++;
                        newAlerts.Add(new AttentionAlertViewModel { ClassId = teacherClass.ClassID, StudentId = studentId, StudentName = studentName, ClassName = classString, AbsencesText = warningReason, IsCritical = false });
                    }
                    else if (isPassed)
                    {
                        passedCount++; 
                    }
                    // NOTE: If they haven't finished midterms yet and have no FDA warnings, they simply remain in 'totalCount' (In Progress).
                }

                newCards.Add(new DashboardClassCardViewModel(teacherClass, totalCount, passedCount, atRiskCount, failedCount));
            }
            
            NeedsAttention = riskCounter;
            ClassCards = newCards; 
            AttentionAlerts = new ObservableCollection<AttentionAlertViewModel>(newAlerts);
            
            // Property for Criticals
            CriticalAlerts = new ObservableCollection<AttentionAlertViewModel>(newCriticalAlerts); 
            HasNoCriticalAlerts = CriticalAlerts.Count == 0;
            OnPropertyChanged(nameof(CriticalAlerts));
            OnPropertyChanged(nameof(HasNoCriticalAlerts));
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
                var gradebookVM = new GradebookViewModel(_dbService);
                gradebookVM.Initialize(alert.ClassId, alert.ClassName, 0, alert.StudentId);
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
        public int ClassId { get; set; } 
        public string StudentId { get; set; } = string.Empty; 
        public string StudentName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string AbsencesText { get; set; } = string.Empty;
        public bool IsCritical { get; set; } = false;
    }

    public partial class TermGradeItem : ObservableObject
    {
        public string TermLabel { get; set; } = string.Empty;
        public string GradeDisplay { get; set; } = "--";
        public bool IsFailing { get; set; } = false; 
    }
}