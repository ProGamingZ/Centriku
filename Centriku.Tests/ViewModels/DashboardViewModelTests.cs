using Xunit;
using System.Threading.Tasks;
using Centriku.Models;
using Centriku.ViewModels;
using Centriku.Tests.Factories;
using System.Linq;

namespace Centriku.Tests.ViewModels
{
    public class DashboardViewModelTests
    {
        private async Task PrepareDashboardTablesAsync(FakeDatabaseService fakeDb)
        {
            var db = fakeDb.GetConnection();
            await db.CreateTablesAsync(SQLite.CreateFlags.None,
                typeof(TeacherClass), typeof(GradingTemplate), typeof(GradingCategory),
                typeof(Student), typeof(ClassRoster), typeof(Assessment), 
                typeof(Score), typeof(AttendanceRecord));
        }

        [Fact]
        public async Task LoadDashboardData_ShouldFilterCards_ByGlobalDropdowns()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareDashboardTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            // Insert 3 diverse classes
            await db.InsertAsync(new TeacherClass { ClassID = 1, SubjectName = "Mobile Prog", AcademicYear = "2025-2026", Term = "1st Sem", Program = "BSCS", IsActive = true });
            await db.InsertAsync(new TeacherClass { ClassID = 2, SubjectName = "Web Dev", AcademicYear = "2025-2026", Term = "2nd Sem", Program = "BSIT", IsActive = true });
            await db.InsertAsync(new TeacherClass { ClassID = 3, SubjectName = "Networking", AcademicYear = "2024-2025", Term = "1st Sem", Program = "BSCS", IsActive = true });

            var vm = new DashboardViewModel(fakeDb, _ => { });
            await vm.LoadDashboardDataAsync();

            // 2. ACT & ASSERT
            // Test default (All)
            Assert.Equal(3, vm.ClassCards.Count);

            // Filter by Term
            vm.SelectedTerm = "1st Sem";
            await Task.Delay(50); // WAIT FOR THE MATH ENGINE!
            Assert.Equal(2, vm.ClassCards.Count); 

            // Filter by Program (Stacking filters: 1st Sem AND BSIT)
            vm.SelectedProgram = "BSIT";
            await Task.Delay(50);
            Assert.Empty(vm.ClassCards); // No BSIT classes in 1st Sem

            // Change Program to BSCS (Stacking filters: 1st Sem AND BSCS)
            vm.SelectedProgram = "BSCS";
            await Task.Delay(50);
            Assert.Equal(2, vm.ClassCards.Count);

            // Filter by Year (Stacking: 1st Sem AND BSCS AND 2025-2026)
            vm.SelectedYear = "2025-2026";
            await Task.Delay(50);
            Assert.Single(vm.ClassCards);
            Assert.Equal("Mobile Prog", vm.ClassCards[0].FullTitle);
        }

        [Fact]
        public async Task CalculateDashboardMetrics_ShouldCountFailingStudents_AndGenerateDeepLinks()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareDashboardTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            // Setup Template & Category (Passing Grade = 75, Category Weight = 100%)
            await db.InsertAsync(new GradingTemplate { TemplateID = 1, PassingGrade = 75, NrfgBaseValue = 50 });
            await db.InsertAsync(new GradingCategory { CategoryID = 1, TemplateID = 1, Name = "Exams", Weight = 100 });

            // Setup Class & Roster
            await db.InsertAsync(new TeacherClass { ClassID = 1, SubjectName = "Test Class", GradingTemplateID = 1, IsActive = true, Program = "BSCS", SectionLabel = "3A" });
            
            await db.InsertAsync(new Student { StudentID = "101", LastName = "Smart", FirstName = "Student" });
            await db.InsertAsync(new Student { StudentID = "102", LastName = "Failing", FirstName = "Student" });
            
            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "101" });
            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "102" });

            // Setup Assessment (100 points Max)
            await db.InsertAsync(new Assessment { AssessmentID = 1, ClassID = 1, Category = "Exams", GradingPeriod = "Midterm", MaxScore = 100 });

            // Setup Scores: Student 1 gets 100 (Pass), Student 2 gets 20 (Fail - Transmutes to 60%)
            await db.InsertAsync(new Score { AssessmentID = 1, StudentID = "101", PointsEarned = 100 });
            await db.InsertAsync(new Score { AssessmentID = 1, StudentID = "102", PointsEarned = 20 });

            var vm = new DashboardViewModel(fakeDb, _ => { });

            // 2. ACT
            await vm.LoadDashboardDataAsync();

            // 3. ASSERT - Quick Access Card Validation
            Assert.Single(vm.ClassCards);
            var card = vm.ClassCards[0];
            Assert.Equal(2, int.Parse(card.StudentCountText.Split(' ')[0])); // 2 Students Total
            Assert.True(card.HasFailingStudents); // The UI warning pill should be visible
            Assert.Equal("1 At-Risk", card.FailingCountText); // Only 1 student is failing

            // 4. ASSERT - Early Warning Deep-Link Validation
            Assert.Single(vm.AttentionAlerts);
            var alert = vm.AttentionAlerts[0];
            
            Assert.Equal("Failing, Student", alert.StudentName); // Correct student caught
            Assert.Equal("Test Class (BSCS_3A)", alert.ClassName); // Correct UI formatting
            
            // Validate the Deep-Link IDs are perfectly attached
            Assert.Equal(1, alert.ClassId);
            Assert.Equal("102", alert.StudentId);
        }
    }
}