using Xunit;
using System.Collections.Generic;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.Tests.Services
{
    public class GradeCalculationServiceTests
    {
        [Fact]
        public void EvaluateFinalGrade_ShouldCalculateCorrectly_AndPassStudent()
        {
            // 1. ARRANGE
            var template = new GradingTemplate { NrfgBaseValue = 50, PassingGrade = 75 };
            
            // 100% of the grade comes from Exams
            var categories = new List<GradingCategory> { new GradingCategory { Name = "Exam", Weight = 100 } };
            
            // There is one 100-item exam
            var termAssessments = new List<Assessment> { new Assessment { AssessmentID = 1, Category = "Exam", MaxScore = 100 } };
            
            // The student scored 50/100
            var studentScores = new List<Score> { new Score { AssessmentID = 1, PointsEarned = 50 } };

            // 2. ACT
            var result = GradeCalculationService.EvaluateFinalGrade(categories, termAssessments, studentScores, template);

            // 3. ASSERT
            // Math: (50/100) * (100 - 50 Base) + 50 Base = 75%
            Assert.Equal(75.0, result.TermNumericGrade);
            Assert.Equal("75%", result.FinalOutput);
            
            // 75 meets the 75 PassingGrade threshold, so they are NOT failing
            Assert.False(result.IsFailing);
        }

        [Fact]
        public void EvaluateFinalGrade_ShouldFlagStudentAsFailing_WhenBelowThreshold()
        {
            // 1. ARRANGE
            var template = new GradingTemplate { NrfgBaseValue = 50, PassingGrade = 75 };
            var categories = new List<GradingCategory> { new GradingCategory { Name = "Exam", Weight = 100 } };
            var termAssessments = new List<Assessment> { new Assessment { AssessmentID = 1, Category = "Exam", MaxScore = 100 } };
            
            // The student scored 40/100
            var studentScores = new List<Score> { new Score { AssessmentID = 1, PointsEarned = 40 } };

            // 2. ACT
            var result = GradeCalculationService.EvaluateFinalGrade(categories, termAssessments, studentScores, template);

            // 3. ASSERT
            // Math: (40/100) * 50 + 50 = 70%
            Assert.Equal(70.0, result.TermNumericGrade);
            Assert.Equal("70%", result.FinalOutput);
            
            // 70 is less than 75, so the dashboard alert MUST trigger
            Assert.True(result.IsFailing);
        }
    }
}