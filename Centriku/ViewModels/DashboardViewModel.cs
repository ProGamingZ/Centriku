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
        [ObservableProperty] public partial int ActiveClasses { get; set; } = 0;
        [ObservableProperty] public partial int NeedsAttention { get; set; } = 0;
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
            var activeRosters = _allRosters.Where(r => activeClassIds.Contains(r.ClassID)).ToList();
            TotalStudents = activeRosters.Select(r => r.StudentID).Distinct().Count();

            // 3. COMBINED ENGINE: Calculate Alerts & Build Cards Simultaneously
            var newCards = new ObservableCollection<DashboardClassCardViewModel>();
            var newAlerts = new List<AttentionAlertViewModel>();
            int riskCounter = 0;
            
            foreach (var teacherClass in activeClassList)
            {
                var template = _allTemplates.FirstOrDefault(t => t.TemplateID == teacherClass.GradingTemplateID);
                var classCategories = _allCategories.Where(c => c.TemplateID == teacherClass.GradingTemplateID).ToList();

                var allClassAssessments = _allAssessments.Where(a => a.ClassID == teacherClass.ClassID).ToList();
                var allClassAssessmentIds = allClassAssessments.Select(a => a.AssessmentID).ToList();
                var classScores = _allScores.Where(s => allClassAssessmentIds.Contains(s.AssessmentID)).ToList();
                var classStudents = activeRosters.Where(r => r.ClassID == teacherClass.ClassID).Select(r => r.StudentID).ToList();

                int failingInThisClassCount = 0; // NEW: Tracker for the Quick Access Card

                foreach (var studentId in classStudents)
                {
                    if (string.IsNullOrEmpty(studentId)) continue;

                    var studentScores = classScores.Where(s => s.StudentID == studentId).ToList();
                    var termGradesList = new List<TermGradeItem>();
                    bool triggersWarning = false;

                    var termsToEvaluate = new List<string> { "Midterm", "Final" };
                    double sumOfRawTerms = 0;
                    int completedTermsCount = 0;

                    foreach (var term in termsToEvaluate)
                    {
                        var termAssessments = allClassAssessments.Where(a => a.GradingPeriod == term).ToList();
                        if (termAssessments.Count == 0)
                        {
                            termGradesList.Add(new TermGradeItem { TermLabel = term, GradeDisplay = "--" });
                            continue;
                        }

                        bool isTerm100Percent = classCategories.All(c => termAssessments.Any(a => a.Category == c.Name && a.MaxScore > 0));
                        var termResult = GradeCalculationService.EvaluateFinalGrade(classCategories, termAssessments, studentScores, template ?? new GradingTemplate());

                        bool isFailingTerm = isTerm100Percent && termResult.IsFailing;
                        if (isFailingTerm) triggersWarning = true;

                        termGradesList.Add(new TermGradeItem { TermLabel = term, GradeDisplay = termResult.FinalOutput, IsFailing = isFailingTerm });

                        if (isTerm100Percent) 
                        {
                            sumOfRawTerms += termResult.TermNumericGrade;
                            completedTermsCount++;
                        }
                    }

                    string finalLabel = "Sem. Avg";
                    TermGradeItem finalGradeItem = new TermGradeItem { TermLabel = finalLabel, GradeDisplay = "--" };

                    if (completedTermsCount == termsToEvaluate.Count)
                    {
                        double finalAcademicAverage = Math.Round(sumOfRawTerms / termsToEvaluate.Count, 0, MidpointRounding.AwayFromZero);
                        finalGradeItem.GradeDisplay = $"{finalAcademicAverage}%";
                        if (finalAcademicAverage < (template?.PassingGrade ?? 75.0)) 
                        {
                            triggersWarning = true;
                            finalGradeItem.IsFailing = true;
                        }
                    }
                    termGradesList.Add(finalGradeItem);

                    if (triggersWarning)
                    {
                        riskCounter++;
                        failingInThisClassCount++; // NEW: Increment the class tracker

                        var student = _allStudents.FirstOrDefault(s => s.StudentID == studentId);
                        string studentName = student != null ? $"{student.LastName}, {student.FirstName}" : studentId;
                        
                        newAlerts.Add(new AttentionAlertViewModel
                        {
                            ClassId = teacherClass.ClassID, 
                            StudentId = studentId, 
                            StudentName = studentName,
                            // FORMAT UPDATED: BSCS_3C instead of just 3C
                            ClassName = $"{teacherClass.SubjectName} ({teacherClass.Program}_{teacherClass.SectionLabel})", 
                            AbsencesText = "Failing Academic Grade", 
                            IsCritical = false, 
                            TermGrades = new ObservableCollection<TermGradeItem>(termGradesList)
                        });
                    }
                }

                // Generate the Class Card WITH the failing count
                int studentCount = classStudents.Count;
                newCards.Add(new DashboardClassCardViewModel(teacherClass, studentCount, failingInThisClassCount));
            }
            
            NeedsAttention = riskCounter;
            ClassCards = newCards; 
            AttentionAlerts = new ObservableCollection<AttentionAlertViewModel>(newAlerts);
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

    public partial class DashboardClassCardViewModel(TeacherClass teacherClass, int studentCount, int failingCount) : ObservableObject
    {
        public TeacherClass ClassRecord { get; } = teacherClass;
        public string StudentCountText { get; } = $"{studentCount} Enrolled";
        public int FailingCount { get; } = failingCount;
        public bool HasFailingStudents => FailingCount > 0; 
        public string FailingCountText => $"{FailingCount} At-Risk"; 
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
        
        public ObservableCollection<TermGradeItem> TermGrades { get; set; } = new();
    }

    public partial class TermGradeItem : ObservableObject
    {
        public string TermLabel { get; set; } = string.Empty;
        public string GradeDisplay { get; set; } = "--";
        public bool IsFailing { get; set; } = false; 
    }
}