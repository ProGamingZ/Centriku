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
            var template = new GradingTemplate { NrfgBaseValue = 50, PassingGrade = 75 };
            var categories = new List<GradingCategory> { new GradingCategory { Name = "Exam", Weight = 100 } };
            var termAssessments = new List<Assessment> { new Assessment { AssessmentID = 1, Category = "Exam", MaxScore = 100 } };
            var studentScores = new List<Score> { new Score { AssessmentID = 1, PointsEarned = 50 } };

            // NEW: Dummy FDA rules (Disabled) and empty attendance
            var dummyClass = new TeacherClass { IsFdaEnabled = false };
            var dummyAttendance = new List<AttendanceRecord>();

            var result = GradeCalculationService.EvaluateFinalGrade(
                categories, termAssessments, studentScores, template, dummyClass, dummyAttendance);

            Assert.Equal(75.0, result.TermNumericGrade);
            Assert.Equal("75%", result.FinalOutput);
            Assert.False(result.IsFailing);
        }

        [Fact]
        public void EvaluateFinalGrade_ShouldFlagStudentAsFailing_WhenBelowThreshold()
        {
            var template = new GradingTemplate { NrfgBaseValue = 50, PassingGrade = 75 };
            var categories = new List<GradingCategory> { new GradingCategory { Name = "Exam", Weight = 100 } };
            var termAssessments = new List<Assessment> { new Assessment { AssessmentID = 1, Category = "Exam", MaxScore = 100 } };
            var studentScores = new List<Score> { new Score { AssessmentID = 1, PointsEarned = 40 } };

            // NEW: Dummy FDA rules (Disabled) and empty attendance
            var dummyClass = new TeacherClass { IsFdaEnabled = false };
            var dummyAttendance = new List<AttendanceRecord>();

            var result = GradeCalculationService.EvaluateFinalGrade(
                categories, termAssessments, studentScores, template, dummyClass, dummyAttendance);

            Assert.Equal(70.0, result.TermNumericGrade);
            Assert.Equal("70%", result.FinalOutput);
            Assert.True(result.IsFailing);
        }

        [Fact]
        public void EvaluateFinalGrade_ShouldFailStudent_WhenFdaEquivalentRuleTriggered()
        {
            // 1. ARRANGE
            var template = new GradingTemplate { NrfgBaseValue = 50, PassingGrade = 75 };
            var categories = new List<GradingCategory> { new GradingCategory { Name = "Exam", Weight = 100 } };
            var termAssessments = new List<Assessment> { new Assessment { AssessmentID = 1, Category = "Exam", MaxScore = 100 } };
            
            // The student scored 100/100 (Perfect Academic Grade)
            var studentScores = new List<Score> { new Score { AssessmentID = 1, PointsEarned = 100 } };

            // FDA Rules: 10 Hours Max. Lates convert at 0.33 (3 lates = 1 absence)
            var fdaClass = new TeacherClass 
            { 
                IsFdaEnabled = true, FdaRuleType = "Equivalent", 
                FdaMaxAbsenceHours = 10.0, FdaLatePenaltyRatio = 0.33, FdaDropGrade = "5.0"
            };
            
            var attendance = new List<AttendanceRecord>
            {
                new AttendanceRecord { Status = "A", SessionHours = 9.0 },
                // Math: 4.0 * 0.33 = 1.32. Total = 10.32 hrs (Breaches the 10.0 limit!)
                new AttendanceRecord { Status = "L", SessionHours = 4.0 }
            };

            // 2. ACT
            var result = GradeCalculationService.EvaluateFinalGrade(categories, termAssessments, studentScores, template, fdaClass, attendance);

            // 3. ASSERT
            Assert.True(result.IsFailing);
            Assert.True(result.IsFda); // Must flag as an attendance drop
            Assert.Equal(0, result.TermNumericGrade);
            Assert.Equal("5.0", result.FinalOutput);
        }

        [Fact]
        public void EvaluateFinalGrade_ShouldFailStudent_WhenFdaIndependentRuleTriggered()
        {
            // 1. ARRANGE
            var template = new GradingTemplate { NrfgBaseValue = 50, PassingGrade = 75 };
            var categories = new List<GradingCategory> { new GradingCategory { Name = "Exam", Weight = 100 } };
            var termAssessments = new List<Assessment> { new Assessment { AssessmentID = 1, Category = "Exam", MaxScore = 100 } };
            var studentScores = new List<Score> { new Score { AssessmentID = 1, PointsEarned = 100 } }; 

            // FDA Rules: Independent. Max Absent = 10, Max Late = 6. 
            var fdaClass = new TeacherClass 
            { 
                IsFdaEnabled = true, FdaRuleType = "Independent", 
                FdaMaxAbsenceHours = 10.0, FdaMaxLateHours = 6.0, FdaDropGrade = "DRP"
            };
            
            // Student only has 2 absent hours (Safe), but has 6 late hours (Breach!)
            var attendance = new List<AttendanceRecord>
            {
                new AttendanceRecord { Status = "A", SessionHours = 2.0 },
                new AttendanceRecord { Status = "L", SessionHours = 6.0 } 
            };

            // 2. ACT
            var result = GradeCalculationService.EvaluateFinalGrade(categories, termAssessments, studentScores, template, fdaClass, attendance);

            // 3. ASSERT
            Assert.True(result.IsFailing);
            Assert.True(result.IsFda);
            Assert.Equal("DRP", result.FinalOutput); // Validates custom drop grade works
        }


    }
}