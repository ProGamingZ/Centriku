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
    
        [Fact]
        public async Task ToggleClassState_ShouldMoveClass_BetweenActiveAndInactiveLists()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareClassTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            // Insert an active class
            await db.InsertAsync(new TeacherClass { ClassID = 1, SubjectName = "Toggle Test", IsActive = true });
            
            var vm = new MyClassesViewModel(fakeDb, _ => { });
            await vm.RefreshDataAsync();

            // Grab the card from the active list
            var targetCard = vm.ActiveClasses[0];

            // 2. ACT
            await vm.ToggleClassStateCommand.ExecuteAsync(targetCard);

            // 3. ASSERT
            // Prove the UI lists updated instantly
            Assert.Empty(vm.ActiveClasses);
            Assert.Single(vm.InactiveClasses);
            Assert.Equal("Toggle Test", vm.InactiveClasses[0].SubjectName);

            // Prove the Database updated securely
            var dbClass = await db.Table<TeacherClass>().FirstAsync();
            Assert.False(dbClass.IsActive);
        }

        [Fact]
        public async Task ConfirmDuplicate_ShouldCloneClassAndRoster_ButNotAssessments()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareClassTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            // Setup Original Class with mixed data
            await db.InsertAsync(new TeacherClass { ClassID = 1, SubjectName = "Original Class" });
            
            // This SHOULD be copied
            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "101" });
            
            // This MUST NOT be copied
            await db.InsertAsync(new Assessment { ClassID = 1, Title = "Midterm" }); 

            var vm = new MyClassesViewModel(fakeDb, _ => { });
            await vm.RefreshDataAsync();

            var targetCard = vm.ActiveClasses[0];
            
            // Trigger the UI modal
            vm.DuplicateClassCommand.Execute(targetCard);

            // 2. ACT
            await vm.ConfirmDuplicateCommand.ExecuteAsync(null);

            // 3. ASSERT
            var classes = await db.Table<TeacherClass>().ToListAsync();
            var rosters = await db.Table<ClassRoster>().ToListAsync();
            var assessments = await db.Table<Assessment>().ToListAsync();

            // Prove the class was duplicated with the correct "(Copy)" suffix
            Assert.Equal(2, classes.Count);
            Assert.Contains(classes, c => c.SubjectName == "Original Class (Copy)");

            // Prove the student roster was safely cloned to the new class (ClassID 2)
            Assert.Equal(2, rosters.Count);
            Assert.Contains(rosters, r => r.ClassID == 2 && r.StudentID == "101");

            // Prove the Assessment was strictly ignored (still only 1 original assessment in the DB)
            Assert.Single(assessments);
            Assert.Equal(1, assessments[0].ClassID);
        }

    }
}