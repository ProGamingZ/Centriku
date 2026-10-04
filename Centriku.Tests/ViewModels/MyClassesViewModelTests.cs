using Xunit;
using System.Threading.Tasks;
using Centriku.Models;
using Centriku.ViewModels;
using Centriku.Tests.Factories;

namespace Centriku.Tests.ViewModels
{
    public class MyClassesViewModelTests
    {
        private async Task PrepareClassTablesAsync(FakeDatabaseService fakeDb)
        {
            var db = fakeDb.GetConnection();
            await db.CreateTablesAsync(SQLite.CreateFlags.None,
                typeof(TeacherClass), typeof(GradingTemplate), typeof(ClassRoster),
                typeof(Assessment), typeof(Score),
                typeof(AttendanceRecord), typeof(RecitationQuestion),
                typeof(AssessmentGroup), typeof(AssessmentGroupMember));
        }

        [Fact]
        public async Task ConfirmDelete_ShouldWipeClass_AndCascadeDeleteAllOrphanedData()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareClassTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            await db.InsertAsync(new TeacherClass { ClassID = 1, SubjectName = "Target Class" });
            await db.InsertAsync(new TeacherClass { ClassID = 2, SubjectName = "Safe Class" });

            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "101" });
            
            // FIX: Let SQLite generate the Assessment, then capture its real ID!
            var testAssessment = new Assessment { ClassID = 1 };
            await db.InsertAsync(testAssessment);
            
            // Use the real ID for the Score
            await db.InsertAsync(new Score { AssessmentID = testAssessment.AssessmentID, StudentID = "101", PointsEarned = 100 });

            await db.InsertAsync(new ClassRoster { ClassID = 2, StudentID = "102" });

            var vm = new MyClassesViewModel(fakeDb, _ => { });
            await vm.RefreshDataAsync();

            var targetCard = vm.ActiveClasses[0]; 
            vm.DeleteClassCommand.Execute(targetCard);
            
            // 2. ACT
            await vm.ConfirmDeleteCommand.ExecuteAsync(null);

            // 3. ASSERT
            var classes = await db.Table<TeacherClass>().ToListAsync();
            var rosters = await db.Table<ClassRoster>().ToListAsync();
            var assessments = await db.Table<Assessment>().ToListAsync();
            var scores = await db.Table<Score>().ToListAsync();

            // Prove Class 1 is gone, but Class 2 survived
            Assert.Single(classes);
            Assert.Equal(2, classes[0].ClassID);

            // Prove Class 1's Roster is gone, but Class 2's Roster survived
            Assert.Single(rosters);
            Assert.Equal(2, rosters[0].ClassID);

            // Prove the cascading ORM safely wiped the related Assessments and Scores
            Assert.Empty(assessments);
            Assert.Empty(scores);
        }
    }
}