using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
    public partial class DirectoryViewModel 
    {
        [ObservableProperty] public partial bool IsProfileOpen { get; set; } = false;
        [ObservableProperty] public partial StudentRowViewModel? SelectedProfile { get; set; }
        
        [ObservableProperty] public partial ObservableCollection<StudentClassPerformanceViewModel> SelectedStudentClasses { get; set; } = new();
        [ObservableProperty] public partial bool HasEnrolledClasses { get; set; } = false;
        [ObservableProperty] public partial StudentClassPerformanceViewModel? SelectedStudentClassPerformance { get; set; }

        private async Task ViewProfileAsync(StudentRowViewModel row)
        {
            if (row == null) return;
            SelectedProfile = row;
            IsProfileOpen = true;
            await LoadStudentPerformanceAsync(row.StudentID);
        }

        private async Task LoadStudentPerformanceAsync(string studentId)
        {
            var db = _dbService.GetConnection();
            var performanceList = new List<StudentClassPerformanceViewModel>();

            var rosters = await db.Table<Centriku.Models.ClassRoster>().Where(r => r.StudentID == studentId).ToListAsync();
            if (!rosters.Any())
            {
                SelectedStudentClasses.Clear();
                SelectedStudentClassPerformance = null;
                HasEnrolledClasses = false;
                return;
            }

            var classIds = rosters.Select(r => r.ClassID).ToHashSet();
            var allClasses = (await db.Table<Centriku.Models.TeacherClass>().ToListAsync()).Where(c => classIds.Contains(c.ClassID)).ToList();
            var allTemplates = await db.Table<Centriku.Models.GradingTemplate>().ToListAsync();
            var allCategories = await db.Table<Centriku.Models.GradingCategory>().ToListAsync();
            var allAssessments = (await db.Table<Centriku.Models.Assessment>().ToListAsync()).Where(a => classIds.Contains(a.ClassID)).ToList();
            var studentScores = await db.Table<Centriku.Models.Score>().Where(s => s.StudentID == studentId).ToListAsync();
            var studentAttendance = await db.Table<Centriku.Models.AttendanceRecord>().Where(a => a.StudentID == studentId).ToListAsync();

            foreach (var tClass in allClasses)
            {
                var template = allTemplates.FirstOrDefault(t => t.TemplateID == tClass.GradingTemplateID) ?? new Centriku.Models.GradingTemplate();
                var classCategories = allCategories.Where(c => c.TemplateID == tClass.GradingTemplateID).ToList();
                var classAssessments = allAssessments.Where(a => a.ClassID == tClass.ClassID).ToList();
                var classAtt = studentAttendance.Where(a => a.ClassID == tClass.ClassID).ToList();

                double absentHours = Math.Round(classAtt.Where(a => a.Status == "Absent" || a.Status == "A").Sum(a => a.SessionHours), 2);
                double lateHours = Math.Round(classAtt.Where(a => a.Status == "Late" || a.Status == "L").Sum(a => a.SessionHours), 2);

                var midAssessments = classAssessments.Where(a => string.Equals((a.GradingPeriod ?? "").Trim(), "Midterm", StringComparison.OrdinalIgnoreCase)).ToList();
                var finAssessments = classAssessments.Where(a => string.Equals((a.GradingPeriod ?? "").Trim(), "Final", StringComparison.OrdinalIgnoreCase)).ToList();

                string midDisplay = "--";
                string finDisplay = "--";
                string semDisplay = "--";

                bool isMidComplete = midAssessments.Any() &&
                                     classCategories.All(c => midAssessments.Any(a => string.Equals((a.Category ?? "").Trim(), (c.Name ?? "").Trim(), StringComparison.OrdinalIgnoreCase))) &&
                                     midAssessments.All(a => studentScores.Any(s => s.AssessmentID == a.AssessmentID));

                bool isFinComplete = finAssessments.Any() &&
                                     classCategories.All(c => finAssessments.Any(a => string.Equals((a.Category ?? "").Trim(), (c.Name ?? "").Trim(), StringComparison.OrdinalIgnoreCase))) &&
                                     finAssessments.All(a => studentScores.Any(s => s.AssessmentID == a.AssessmentID));

                var midResult = Centriku.Services.GradeCalculationService.EvaluateFinalGrade(classCategories, midAssessments, studentScores, template, tClass, classAtt);
                var finResult = Centriku.Services.GradeCalculationService.EvaluateFinalGrade(classCategories, finAssessments, studentScores, template, tClass, classAtt);

                if (midAssessments.Any()) midDisplay = midResult.FinalOutput;
                if (finAssessments.Any()) finDisplay = finResult.FinalOutput;

                if (midResult.IsFda || finResult.IsFda)
                {
                    semDisplay = tClass.FdaDropGrade;
                }
                else if (isMidComplete && isFinComplete)
                {
                    double semAvg = Math.Round((midResult.TermNumericGrade + finResult.TermNumericGrade) / 2.0, 0, MidpointRounding.AwayFromZero);
                    semDisplay = $"{semAvg}%";
                }

                performanceList.Add(new StudentClassPerformanceViewModel
                {
                    SubjectName = tClass.SubjectName ?? "Unknown Subject",
                    Term = tClass.Term ?? "Unknown Term",
                    Absences = absentHours,
                    Lates = lateHours,
                    MidtermGrade = midDisplay,
                    FinalTermGrade = finDisplay,
                    SemesterAverage = semDisplay,
                    AverageScorePercentage = semDisplay,
                    GradedTasksCount = classAssessments.Count
                });
            }

            SelectedStudentClasses = new ObservableCollection<StudentClassPerformanceViewModel>(performanceList);
            SelectedStudentClassPerformance = performanceList.FirstOrDefault();
            HasEnrolledClasses = performanceList.Any();
        }
    }
}