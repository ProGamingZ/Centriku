using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Centriku.Services;
using Centriku.Models;
using System.Linq;
using System.Collections.Generic;

namespace Centriku.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
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

        // IN-MEMORY CACHE (Prevents database lag!) 
        private List<TeacherClass> _allClasses = [];
        private List<ClassRoster> _allRosters = [];
        private List<GradingTemplate> _allTemplates = [];
        private List<Assessment> _allAssessments = [];
        private List<Score> _allScores = [];
        private List<AttendanceRecord> _allAttendance = [];
        private List<GradingCategory> _allCategories = [];
        private List<GradeBoundary> _allBoundaries = [];

        public IRelayCommand<DashboardClassCardViewModel> OpenClassCommand { get; }

        public DashboardViewModel(System.Action<ViewModelBase> navigateAction)
        {
            _navigateAction = navigateAction;
            OpenClassCommand = new RelayCommand<DashboardClassCardViewModel>(OpenClass!);
        }

        public async Task LoadDashboardDataAsync()
        {
            // 1. Fetch ALL data exactly ONCE when the dashboard loads
            var db = new DatabaseService().GetConnection();
            _allClasses = await db.Table<TeacherClass>().ToListAsync();
            _allStudents = await db.Table<Student>().ToListAsync();
            _allRosters = await db.Table<ClassRoster>().ToListAsync();
            _allTemplates = await db.Table<GradingTemplate>().ToListAsync();
            _allAssessments = await db.Table<Assessment>().ToListAsync();
            _allScores = await db.Table<Score>().ToListAsync();
            _allAttendance = await db.Table<AttendanceRecord>().ToListAsync();
            _allCategories = await db.Table<GradingCategory>().ToListAsync();
            _allBoundaries = await db.Table<GradeBoundary>().ToListAsync();

            // 2. Dynamically extract the unique Years and Terms from the teacher's classes
            var years = _allClasses.Select(c => c.AcademicYear).Where(y => !string.IsNullOrWhiteSpace(y)).Distinct().OrderByDescending(y => y).ToList();
            AvailableYears.Clear(); 
            AvailableYears.Add("All Years");
            foreach (var y in years) AvailableYears.Add(y!);
            if (!AvailableYears.Contains(SelectedYear)) SelectedYear = "All Years";

            var terms = _allClasses.Select(c => c.Term).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().OrderBy(t => t).ToList();
            AvailableTerms.Clear(); 
            AvailableTerms.Add("All Terms");
            foreach (var t in terms) AvailableTerms.Add(t!);
            if (!AvailableTerms.Contains(SelectedTerm)) SelectedTerm = "All Terms";

            // 3. Run the math engine using our new cache
            await CalculateDashboardMetricsAsync();
        }

        private async Task CalculateDashboardMetricsAsync()
        {
            // Yield the UI thread briefly so the dropdown animation finishes smoothly
            await Task.Delay(20);

            // 1. FILTER THE CLASSES BASED ON DROPDOWNS!
            var filteredClasses = _allClasses.AsEnumerable();
            
            if (SelectedYear != "All Years") 
                filteredClasses = filteredClasses.Where(c => c.AcademicYear == SelectedYear);
            
            if (SelectedTerm != "All Terms") 
                filteredClasses = filteredClasses.Where(c => c.Term == SelectedTerm);

            var activeClassList = filteredClasses.ToList();
            var activeClassIds = activeClassList.Select(c => c.ClassID).ToList();

            // 2. Calculate KPI: Active Classes
            ActiveClasses = activeClassList.Count;

            // 3. Calculate KPI: Total Students (Only counts students in the filtered classes!)
            var activeRosters = _allRosters.Where(r => activeClassIds.Contains(r.ClassID)).ToList();
            TotalStudents = activeRosters.Select(r => r.StudentID).Distinct().Count();

            // 4. Generate the Quick Access Cards
            var newCards = new ObservableCollection<DashboardClassCardViewModel>();
            foreach (var teacherClass in activeClassList)
            {
                int studentCount = activeRosters.Count(r => r.ClassID == teacherClass.ClassID);
                newCards.Add(new DashboardClassCardViewModel(teacherClass, studentCount));
            }
            ClassCards = newCards; // Assigning a fresh list prevents UI glitches!

            // 5. Calculate KPI: Needs Attention (The Math Engine)
            var newAlerts = new List<AttentionAlertViewModel>();
            int riskCounter = 0;
            foreach (var teacherClass in activeClassList)
            {
                var template = _allTemplates.FirstOrDefault(t => t.TemplateID == teacherClass.GradingTemplateID);
                
                var classCategories = _allCategories.Where(c => c.TemplateID == teacherClass.GradingTemplateID).ToList();
                var classBoundaries = _allBoundaries.Where(b => b.TemplateID == teacherClass.GradingTemplateID).ToList();


                // 1. Get ALL assessments for the class
                var allClassAssessments = _allAssessments.Where(a => a.ClassID == teacherClass.ClassID).ToList();
                var allClassAssessmentIds = allClassAssessments.Select(a => a.AssessmentID).ToList();

                // 2. Fetch ALL scores for the entire year so the Mini-Report Card can read them!
                var classScores = _allScores.Where(s => allClassAssessmentIds.Contains(s.AssessmentID)).ToList();

                var classAttendance = _allAttendance.Where(a => a.ClassID == teacherClass.ClassID).ToList();
                var classStudents = activeRosters.Where(r => r.ClassID == teacherClass.ClassID).Select(r => r.StudentID).ToList();

                foreach (var studentId in classStudents)
                {
                    if (string.IsNullOrEmpty(studentId)) continue;

                    var studentScores = classScores.Where(s => s.StudentID == studentId).ToList();
                    var termGradesList = new List<TermGradeItem>();
                    bool triggersWarning = false;
                    bool isCriticalFA = false;

                    // 1. Determine which terms to show based on Education Mode
                    var termsToEvaluate = teacherClass.EducationMode == "Semestral" 
                        ? new List<string> { "Midterm", "Final" } 
                        : new List<string> { "Q1", "Q2", "Q3", "Q4" };

                    double sumOfRawTerms = 0;
                    int completedTermsCount = 0;

                    // 2. Evaluate every individual term
                    foreach (var term in termsToEvaluate)
                    {
                        var termAssessments = allClassAssessments.Where(a => a.GradingPeriod == term).ToList();
                        
                        if (termAssessments.Count == 0)
                        {
                            termGradesList.Add(new TermGradeItem { TermLabel = term, GradeDisplay = "--" });
                            continue;
                        }

                        // SMART CHECK: Does this term have at least one assessment in EVERY category? (Total weight = 100%)
                        bool isTerm100Percent = classCategories.All(c => termAssessments.Any(a => a.Category == c.Name && a.MaxScore > 0));

                        double rawAcademicGrade = GradeCalculationService.CalculateRawAcademicGrade(
                            classCategories, termAssessments, studentScores);

                        // Calculate Transmuted Grade (Bypass attendance for single terms)
                        var tempClass = new TeacherClass { AttendanceCalculationMode = "None" };
                        var termResult = GradeCalculationService.EvaluateFinalGrade(
                            rawAcademicGrade, tempClass, template ?? new GradingTemplate(), classBoundaries, 0, 0);

                        // Only flag as red if it is completely graded AND failing!
                        bool isFailingTerm = isTerm100Percent && termResult.IsFailing;
                        if (isFailingTerm) triggersWarning = true;

                        termGradesList.Add(new TermGradeItem 
                        { 
                            TermLabel = term, 
                            GradeDisplay = termResult.FinalOutput, 
                            IsFailing = isFailingTerm 
                        });

                        // Store for the final average calculation
                        if (isTerm100Percent) 
                        {
                            sumOfRawTerms += rawAcademicGrade;
                            completedTermsCount++;
                        }
                    }

                    // 3. Process Attendance
                    var studentAttendance = classAttendance.Where(a => a.StudentID == studentId).ToList();
                    int totalDays = studentAttendance.Select(a => a.Date.Date).Distinct().Count();
                    int totalA = studentAttendance.Count(a => a.Status == "A");
                    int totalL = studentAttendance.Count(a => a.Status == "L");
                    int totalE = studentAttendance.Count(a => a.Status == "E"); 
                    
                    int activeDays = totalDays - totalE;
                    double effectiveAbsences = totalA + (totalL * teacherClass.LateValue);

                    // 4. Calculate Final Average
                    string finalLabel = teacherClass.EducationMode == "Semestral" ? "Sem. Avg" : "Final Avg";
                    TermGradeItem finalGradeItem = new TermGradeItem { TermLabel = finalLabel, GradeDisplay = "--" };

                    // Apply strict Threshold FA failure even if the year isn't over yet
                    if (teacherClass.AttendanceCalculationMode == "Threshold" && effectiveAbsences >= teacherClass.MaxAbsencesAllowed)
                    {
                        triggersWarning = true;
                        isCriticalFA = true;
                        finalGradeItem.GradeDisplay = "FA";
                        finalGradeItem.IsFailing = true;
                    }
                    // Only calculate actual final math if ALL periods reached 100%
                    else if (completedTermsCount == termsToEvaluate.Count)
                    {
                        double finalAcademicAverage = sumOfRawTerms / termsToEvaluate.Count;

                        var finalResult = GradeCalculationService.EvaluateFinalGrade(
                            finalAcademicAverage, teacherClass, template ?? new GradingTemplate(), classBoundaries, activeDays, effectiveAbsences);
                        
                        finalGradeItem.GradeDisplay = finalResult.IsFA ? "FA" : finalResult.FinalOutput;
                        
                        if (finalResult.IsFailing || finalResult.IsFA) 
                        {
                            triggersWarning = true;
                            finalGradeItem.IsFailing = true;
                            if (finalResult.IsFA) isCriticalFA = true;
                        }
                    }

                    termGradesList.Add(finalGradeItem);

                    // 5. Package the warning for the UI!
                    if (triggersWarning)
                    {
                        riskCounter++;
                        var student = _allStudents.FirstOrDefault(s => s.StudentID == studentId);
                        string studentName = student != null ? $"{student.LastName}, {student.FirstName}" : studentId;
                        
                        newAlerts.Add(new AttentionAlertViewModel
                        {
                            StudentName = studentName,
                            ClassName = $"{teacherClass.SubjectName} ({teacherClass.SectionLabel})",
                            AbsencesText = $"Absences: {effectiveAbsences}",
                            IsCritical = isCriticalFA,
                            TermGrades = new ObservableCollection<TermGradeItem>(termGradesList)
                        });
                    }
                }
            }
            NeedsAttention = riskCounter;
            AttentionAlerts = new ObservableCollection<AttentionAlertViewModel>(newAlerts);
        }

        private void OpenClass(DashboardClassCardViewModel selectedClass)
        {
            if (selectedClass != null)
            {
                var gradebookVM = new GradebookViewModel();
                gradebookVM.Initialize(selectedClass.ClassRecord.ClassID, selectedClass.ClassRecord.SubjectName ?? string.Empty);
                _navigateAction(gradebookVM);
            }
        }
    }

    public partial class DashboardClassCardViewModel : ObservableObject
    {
        public TeacherClass ClassRecord { get; }
        public string StudentCountText { get; }
        
        public string FullTitle => $"{ClassRecord.SubjectName} - {ClassRecord.SectionLabel}";
        public string Subtitle => $"{ClassRecord.Term} | {ClassRecord.AcademicYear}";

        public DashboardClassCardViewModel(TeacherClass teacherClass, int studentCount)
        {
            ClassRecord = teacherClass;
            StudentCountText = $"{studentCount} Students Enrolled";
        }
    }
    public partial class AttentionAlertViewModel : ObservableObject
    {
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
        public bool IsFailing { get; set; } = false; // Triggers the Red color in UI
    }
}