using Xunit;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Centriku.Models;
using Centriku.ViewModels;
using Centriku.Tests.Factories;

namespace Centriku.Tests.ViewModels.Gradebook
{
    public class GradebookRecitationTests
    {
        private static async Task PrepareRecitationTablesAsync(FakeDatabaseService fakeDb)
        {
            var db = fakeDb.GetConnection();
            await db.CreateTablesAsync(SQLite.CreateFlags.None,
                typeof(ClassRoster), typeof(RecitationQuestion), typeof(Student));
        }

        [Fact]
        public async Task WheelAnimationCompleted_ShouldMoveStudentToChosen_AndMarkRoster()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareRecitationTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            var student = new Student { StudentID = "S-100", FirstName = "John", LastName = "Doe" };
            await db.InsertAsync(student);
            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "S-100", HasRecited = false });
            await db.InsertAsync(new RecitationQuestion { ClassID = 1, QuestionText = "What is 2+2?", IsIncluded = true });

            var vm = new GradebookViewModel(fakeDb)
            {
                ClassId = 1,
                GradebookRows = [new StudentGradeRow(fakeDb, student)]
            };

            await vm.ResetRecitationCommand.ExecuteAsync(null);

            // 2. ACT
            await vm.WheelAnimationCompletedAsync(0);

            // 3. ASSERT
            Assert.Empty(vm.RemainingStudents);
            Assert.Single(vm.ChosenStudents);
            Assert.Equal("S-100", vm.ChosenStudents[0].StudentID);

            var roster = await db.Table<ClassRoster>().FirstAsync();
            Assert.True(roster.HasRecited);

            Assert.True(vm.IsWinnerModalOpen);
            Assert.NotNull(vm.WinnerModal);
            Assert.Equal("What is 2+2?", vm.WinnerModal.QuestionText);
        }

        [Fact]
        public async Task RestoreStudentToWheel_ShouldMoveStudentBack_AndResetRoster()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareRecitationTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            var student = new Student { StudentID = "S-100", FirstName = "John", LastName = "Doe" };
            await db.InsertAsync(student);
            
            // Start them normally as NOT recited
            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "S-100", HasRecited = false });

            var vm = new GradebookViewModel(fakeDb) { ClassId = 1 };
            vm.GradebookRows = [new(fakeDb, student)];
            
            // Load them into the UI
            await vm.ResetRecitationCommand.ExecuteAsync(null);
            
            // Simulate the wheel spinning to push them into the Chosen list
            await vm.WheelAnimationCompletedAsync(0);
            
            // Verify they are securely in the Chosen list before we test Restore
            Assert.Single(vm.ChosenStudents);
            Assert.Empty(vm.RemainingStudents);

            // 2. ACT (Click the Restore button)
            await vm.RestoreStudentToWheelCommand.ExecuteAsync(vm.ChosenStudents[0]);

            // 3. ASSERT
            // Prove the UI lists were reversed
            Assert.Empty(vm.ChosenStudents);
            Assert.Single(vm.RemainingStudents);

            // Prove the Database flag was safely reversed
            var roster = await db.Table<ClassRoster>().FirstAsync();
            Assert.False(roster.HasRecited);
        }
    }
}