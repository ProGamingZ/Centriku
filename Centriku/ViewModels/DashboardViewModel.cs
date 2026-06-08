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

        // KPI PROPERTIES 
        [ObservableProperty] public partial int TotalStudents { get; set; } = 0;
        [ObservableProperty] public partial int ActiveClasses { get; set; } = 0;
        [ObservableProperty] public partial int NeedsAttention { get; set; } = 0;
        [ObservableProperty] public partial ObservableCollection<DashboardClassCardViewModel> ClassCards { get; set; } = new();

        // GLOBAL FILTER PROPERTIES 
        [ObservableProperty] public partial ObservableCollection<string> AvailableYears { get; set; } = new();
        [ObservableProperty] public partial string SelectedYear { get; set; } = "All Years";
        partial void OnSelectedYearChanged(string value) => _ = CalculateDashboardMetricsAsync();

        [ObservableProperty] public partial ObservableCollection<string> AvailableTerms { get; set; } = new();
        [ObservableProperty] public partial string SelectedTerm { get; set; } = "All Terms";
        partial void OnSelectedTermChanged(string value) => _ = CalculateDashboardMetricsAsync();

        // IN-MEMORY CACHE (Prevents database lag!) 
        private List<TeacherClass> _allClasses = new();
        private List<ClassRoster> _allRosters = new();
        private List<GradingTemplate> _allTemplates = new();
        private List<Assessment> _allAssessments = new();
        private List<Score> _allScores = new();
        private List<AttendanceRecord> _allAttendance = new();

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
            _allRosters = await db.Table<ClassRoster>().ToListAsync();
            _allTemplates = await db.Table<GradingTemplate>().ToListAsync();
            _allAssessments = await db.Table<Assessment>().ToListAsync();
            _allScores = await db.Table<Score>().ToListAsync();
            _allAttendance = await db.Table<AttendanceRecord>().ToListAsync();

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
            int riskCounter = 0;
            foreach (var teacherClass in activeClassList)
            {
                var template = _allTemplates.FirstOrDefault(t => t.TemplateID == teacherClass.GradingTemplateID);
                double passingScore = template?.PassingGrade ?? 75.0;

                var classAssessments = _allAssessments.Where(a => a.ClassID == teacherClass.ClassID).ToList();
                var classAssessmentIds = classAssessments.Select(a => a.AssessmentID).ToList();
                var classScores = _allScores.Where(s => classAssessmentIds.Contains(s.AssessmentID)).ToList();
                var classAttendance = _allAttendance.Where(a => a.ClassID == teacherClass.ClassID).ToList();
                var classStudents = activeRosters.Where(r => r.ClassID == teacherClass.ClassID).Select(r => r.StudentID).ToList();

                foreach (var studentId in classStudents)
                {
                    if (string.IsNullOrEmpty(studentId)) continue;

                    double totalEarned = classScores.Where(s => s.StudentID == studentId).Sum(s => s.PointsEarned);
                    double totalMax = classAssessments.Sum(a => a.MaxScore);

                    double academicGrade = 100.0;
                    if (totalMax > 0) academicGrade = (totalEarned / totalMax) * 100.0;

                    var studentAttendance = classAttendance.Where(a => a.StudentID == studentId).ToList();
                    int totalDays = studentAttendance.Select(a => a.Date.Date).Distinct().Count();
                    int totalA = studentAttendance.Count(a => a.Status == "A");
                    int totalL = studentAttendance.Count(a => a.Status == "L");
                    double effectiveAbsences = totalA + (totalL * teacherClass.LateValue);

                    bool isFailing = false;
                    switch (teacherClass.AttendanceCalculationMode)
                    {
                        case "Threshold":
                            if (effectiveAbsences >= teacherClass.MaxAbsencesAllowed) isFailing = true;
                            else if (academicGrade < passingScore) isFailing = true;
                            break;

                        case "Weighted":
                            double attendanceScore = 100.0;
                            if (totalDays > 0)
                            {
                                attendanceScore = ((totalDays - effectiveAbsences) / totalDays) * 100.0;
                                if (attendanceScore < 0) attendanceScore = 0;
                            }
                            double academicWeight = (100.0 - teacherClass.AttendanceWeight) / 100.0;
                            double attWeight = teacherClass.AttendanceWeight / 100.0;
                            double weightedFinal = (academicGrade * academicWeight) + (attendanceScore * attWeight);
                            if (weightedFinal < passingScore) isFailing = true;
                            break;

                        case "Bonus":
                            double bonusFinal = academicGrade;
                            if (effectiveAbsences == 0 && totalDays > 0) bonusFinal += teacherClass.AttendanceWeight; 
                            else if (effectiveAbsences > teacherClass.MaxAbsencesAllowed) bonusFinal -= teacherClass.AttendanceWeight; 
                            if (bonusFinal < 0) bonusFinal = 0;
                            if (bonusFinal < passingScore) isFailing = true;
                            break;

                        default:
                            if (academicGrade < passingScore) isFailing = true;
                            break;
                    }

                    if (isFailing) riskCounter++;
                }
            }
            NeedsAttention = riskCounter;
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
}