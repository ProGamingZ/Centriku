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
                    SubjectName = tClass.SubjectName ?? "Unknown Subject", Term = tClass.Term ?? "Unknown Term",
                    Absences = absences, Lates = lates
                };

                string FormatGrade(double? raw)
                {
                    if (raw == null) return "--";
                    double val = raw.Value;
                    if (template?.CalculationMode == "NRFG") val = (val / 100.0) * (100.0 - template.NrfgBaseValue) + template.NrfgBaseValue;
                    return $"{val.ToString("0.##")}%";
                }

                // Strictly College Semestral Calculation
                double? mid = await CalculateTermGradeRawAsync(db, studentId, tClass.ClassID, tClass.GradingTemplateID, "Midterm");
                double? fin = await CalculateTermGradeRawAsync(db, studentId, tClass.ClassID, tClass.GradingTemplateID, "Final");
                perf.MidtermGrade = FormatGrade(mid); perf.FinalTermGrade = FormatGrade(fin);
                perf.SemesterAverage = (mid != null && fin != null) ? FormatGrade((mid + fin) / 2.0) : "--";
                perf.AverageScorePercentage = perf.SemesterAverage;

                var allAssessments = await db.Table<Centriku.Models.Assessment>().Where(a => a.ClassID == roster.ClassID).ToListAsync();
                perf.GradedTasksCount = allAssessments.Count;
                performanceList.Add(perf);
            }

            SelectedStudentClasses = new ObservableCollection<StudentClassPerformanceViewModel>(performanceList);
            SelectedStudentClassPerformance = performanceList.FirstOrDefault();
            HasEnrolledClasses = performanceList.Any();
            IsMasterRecordVisible = true;

            if (IsMasterRecordVisible) await LoadMasterRecordAsync(studentId, performanceList);
        }

        private async Task<double?> CalculateTermGradeRawAsync(SQLite.SQLiteAsyncConnection db, string studentId, int classId, int templateId, string term)
        {
            var categories = await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == templateId).ToListAsync();
            var assessments = await db.Table<Centriku.Models.Assessment>().Where(a => a.ClassID == classId && a.GradingPeriod == term).ToListAsync();
            if (!assessments.Any()) return null;

            double totalWeightedScore = 0; double totalCategoryWeight = 0; bool hasAnyAssessments = false;

            foreach (var category in categories)
            {
                double weightDecimal = category.Weight / 100.0;
                totalCategoryWeight += weightDecimal;
                double catEarned = 0; double catMax = 0;

                var catAssessments = assessments.Where(a => a.Category == category.Name).ToList();
                foreach (var assessment in catAssessments)
                {
                    var score = await db.Table<Centriku.Models.Score>().Where(s => s.AssessmentID == assessment.AssessmentID && s.StudentID == studentId).FirstOrDefaultAsync();
                    if (score != null && !score.IsExcused && assessment.MaxScore > 0)
                    {
                        catEarned += score.PointsEarned; catMax += assessment.MaxScore;
                    }
                }
                if (catMax > 0)
                {
                    hasAnyAssessments = true;
                    totalWeightedScore += ((catEarned / catMax) * 100.0 * weightDecimal);
                }
            }
            if (!hasAnyAssessments || totalCategoryWeight == 0) return null;
            return totalWeightedScore / totalCategoryWeight;
        }
    }
}