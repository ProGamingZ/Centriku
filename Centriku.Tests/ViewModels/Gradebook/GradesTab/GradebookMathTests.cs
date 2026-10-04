using Xunit;
using Moq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Centriku.Models;
using Centriku.ViewModels;
using Centriku.Services;

namespace Centriku.Tests.ViewModels.Gradebook
{
    public class GradebookMathTests
    {
        [Fact]
        public void RecalculateFinalGrades_ShouldApplyTransmutationFormula_Correctly()
        {
            // 1. ARRANGE
            var mockDb = new Mock<IDatabaseService>();
            
            var vm = new GradebookViewModel(mockDb.Object)
            {
                NrfgBaseValue = 50.0,
                SelectedTermView = "Midterm",
                ClassAssessments = new ObservableCollection<Assessment>(),
                AvailableCategories = new ObservableCollection<GradingCategory>()
            };
            
            vm.AvailableCategories.Add(new GradingCategory { Name = "Exams", Weight = 100.0 });

            var assessment = new Assessment { AssessmentID = 1, Category = "Exams", GradingPeriod = "Midterm", MaxScore = 100.0 };
            vm.ClassAssessments.Add(assessment);

            var studentInfo = new Student { StudentID = "101", LastName = "Doe" };
            
            // We pass the mockDb we created at the top of this test method
            var gradeRow = new StudentGradeRow(mockDb.Object, studentInfo);
            
            var rawScore = new Score { AssessmentID = 1, StudentID = "101", PointsEarned = 50.0 };
            var scoreCell = new ScoreCellViewModel(mockDb.Object, rawScore, 100.0, () => { });
            
            gradeRow.Scores[1] = scoreCell;

            var gradeRows = new List<StudentGradeRow> { gradeRow };
            var attRows = new List<AttendanceGridRowViewModel>();

            // 2. ACT
            vm.RecalculateFinalGradesForList(gradeRows, attRows);

            // 3. ASSERT
            // Transmutation: (50 earned / 100 max) * (100 - 50 base) + 50 base = 75.0%
            Assert.Equal(75.0, gradeRow.MidtermGradeNumeric);
            Assert.Equal("75%", gradeRow.MidtermGradeDisplay);
            Assert.Equal("75%", gradeRow.FinalGrade);
        }

        [Fact]
        public void RecalculateFinalGrades_ShouldReturnIncomplete_WhenCategoryIsMissing()
        {
            // 1. ARRANGE
            var mockDb = new Mock<IDatabaseService>();
            
            var vm = new GradebookViewModel(mockDb.Object)
            {
                NrfgBaseValue = 0.0,
                SelectedTermView = "Midterm",
                ClassAssessments = new ObservableCollection<Assessment>(),
                AvailableCategories = new ObservableCollection<GradingCategory>()
            };
            
            vm.AvailableCategories.Add(new GradingCategory { Name = "Projects", Weight = 100.0 });
            
            var studentInfo = new Student { StudentID = "101" };
            
            // We pass the mockDb we created at the top of this test method
            var gradeRow = new StudentGradeRow(mockDb.Object, studentInfo);
            var gradeRows = new List<StudentGradeRow> { gradeRow };

            // 2. ACT
            vm.RecalculateFinalGradesForList(gradeRows, new List<AttendanceGridRowViewModel>());

            // 3. ASSERT
            Assert.Equal(0, gradeRow.MidtermGradeNumeric);
            Assert.Equal("--", gradeRow.MidtermGradeDisplay);
            Assert.Contains("No assessments recorded yet.", gradeRow.MidtermComputationTooltip);
        }
    }
}