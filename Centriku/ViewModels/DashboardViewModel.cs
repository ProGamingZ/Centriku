using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Centriku.Services;
using Centriku.Models;
using System.Linq;

namespace Centriku.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly System.Action<ViewModelBase> _navigateAction;

        [ObservableProperty] public partial int TotalStudents { get; set; } = 0;
        [ObservableProperty] public partial int ActiveClasses { get; set; } = 0;
        [ObservableProperty] public partial int NeedsAttention { get; set; } = 0;

        [ObservableProperty] public partial ObservableCollection<DashboardClassCardViewModel> ClassCards { get; set; } = new();

        // === NEW: Navigation commands match MyClasses pattern ===
        public IRelayCommand<DashboardClassCardViewModel> OpenClassCommand { get; }

        public DashboardViewModel(System.Action<ViewModelBase> navigateAction)
        {
            _navigateAction = navigateAction;
            OpenClassCommand = new RelayCommand<DashboardClassCardViewModel>(OpenClass!);
        }
        // ========================================================

        public async Task LoadDashboardDataAsync()
        {
            var db = new DatabaseService().GetConnection();

            ActiveClasses = await db.Table<TeacherClass>().CountAsync();

            var rosters = await db.Table<ClassRoster>().ToListAsync();
            TotalStudents = rosters.Select(r => r.StudentID).Distinct().Count();

            var classes = await db.Table<TeacherClass>().ToListAsync();
            var templates = await db.Table<GradingTemplate>().ToListAsync();
            var allAssessments = await db.Table<Assessment>().ToListAsync();
            var allScores = await db.Table<Score>().ToListAsync();
            var allAttendance = await db.Table<AttendanceRecord>().ToListAsync();
            
            ClassCards.Clear();
            foreach (var teacherClass in classes)
            {
                int studentCount = rosters.Count(r => r.ClassID == teacherClass.ClassID);
                ClassCards.Add(new DashboardClassCardViewModel(teacherClass, studentCount));
            }

            int riskCounter = 0;
            foreach (var teacherClass in classes)
            {
                var template = templates.FirstOrDefault(t => t.TemplateID == teacherClass.GradingTemplateID);
                double passingScore = template?.PassingGrade ?? 75.0;

                var classAssessments = allAssessments.Where(a => a.ClassID == teacherClass.ClassID).ToList();
                var classAssessmentIds = classAssessments.Select(a => a.AssessmentID).ToList();
                var classScores = allScores.Where(s => classAssessmentIds.Contains(s.AssessmentID)).ToList();
                var classAttendance = allAttendance.Where(a => a.ClassID == teacherClass.ClassID).ToList();
                var classStudents = rosters.Where(r => r.ClassID == teacherClass.ClassID).Select(r => r.StudentID).ToList();

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

        // === NEW: Spawns the gradebook workspace matching MyClassesView ===
        private void OpenClass(DashboardClassCardViewModel selectedClass)
        {
            if (selectedClass != null)
            {
                var gradebookVM = new GradebookViewModel();
                gradebookVM.Initialize(selectedClass.ClassRecord.ClassID, selectedClass.ClassRecord.SubjectName ?? string.Empty);
                _navigateAction(gradebookVM);
            }
        }
        // ==================================================================
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