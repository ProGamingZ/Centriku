using Xunit;
using System.Threading.Tasks;
using System.Linq;
using Centriku.Models;
using Centriku.ViewModels;
using Centriku.Tests.Factories;

namespace Centriku.Tests.ViewModels.Directory
{
    public class DirectoryViewModelTests
    {
        private async Task PrepareDirectoryTablesAsync(FakeDatabaseService fakeDb)
        {
            var db = fakeDb.GetConnection();
            await db.CreateTablesAsync(SQLite.CreateFlags.None,
                typeof(Student), typeof(AppSettings), typeof(ClassRoster),
                typeof(Score), typeof(AttendanceRecord), typeof(AssessmentGroupMember));
        }

        [Fact]
        public async Task SaveStudent_ShouldInsertIntoDatabase_AndRefreshUIList()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareDirectoryTablesAsync(fakeDb);
            
            var vm = new DirectoryViewModel(fakeDb);
            await vm.LoadStudentsAsync();

            vm.NewStudentId = "TEST-001";
            vm.NewStudentLastName = "Dela Cruz";
            vm.NewStudentFirstName = "Juan";
            vm.NewStudentProgram = "BSIT";

            // 2. ACT
            await vm.SaveStudentCommand.ExecuteAsync(null); 

            // 3. ASSERT
            var savedStudent = await fakeDb.GetConnection().Table<Student>().FirstOrDefaultAsync();
            
            Assert.NotNull(savedStudent);
            Assert.Equal("TEST-001", savedStudent.StudentID);
            Assert.Single(vm.DisplayedStudents);
            Assert.Equal("Dela Cruz", vm.DisplayedStudents.First().LastName);
            Assert.Empty(vm.NewStudentId);
        }

        [Fact]
        public async Task SearchQuery_ShouldFilterDisplayedStudents_AndKeepSelectedStudentsSticky()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareDirectoryTablesAsync(fakeDb);
            
            var student1 = TestDataFactory.CreateFakeStudent(studentId: "101", lastName: "Smith");
            var student2 = TestDataFactory.CreateFakeStudent(studentId: "102", lastName: "Johnson");
            await fakeDb.GetConnection().InsertAllAsync(new[] { student1, student2 });

            var vm = new DirectoryViewModel(fakeDb); 
            await vm.LoadStudentsAsync();

            // Check "Johnson" before searching for "smith"
            var johnsonRow = vm.DisplayedStudents.First(s => s.LastName == "Johnson");
            johnsonRow.IsSelected = true;

            // 2. ACT
            vm.SearchQuery = "smith";

            // 3. ASSERT: Both "Smith" (matches search) and "Johnson" (sticky selection) remain visible!
            Assert.Equal(2, vm.DisplayedStudents.Count);
            Assert.Contains(vm.DisplayedStudents, s => s.LastName == "Smith");
            Assert.Contains(vm.DisplayedStudents, s => s.LastName == "Johnson");
        }

        [Fact]
        public async Task BulkArchiveAndRestore_ShouldMoveStudents_BetweenMasterAndArchiveLists()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareDirectoryTablesAsync(fakeDb);
            
            var s1 = TestDataFactory.CreateFakeStudent(studentId: "101", lastName: "Alpha");
            var s2 = TestDataFactory.CreateFakeStudent(studentId: "102", lastName: "Beta");
            await fakeDb.GetConnection().InsertAllAsync(new[] { s1, s2 });

            var vm = new DirectoryViewModel(fakeDb); 
            await vm.LoadStudentsAsync();
            
            // Select all students in Master Roster
            vm.IsAllMasterSelected = true;

            // 2. ACT - Archive Batch via Confirmation Modal
            vm.PromptBulkArchiveCommand.Execute(null);
            Assert.True(vm.IsConfirmModalOpen);
            Assert.NotNull(vm.ConfirmModal);
            await vm.ConfirmModal!.ConfirmCommand.ExecuteAsync(null);

            // 3. ASSERT - Moved to Archives
            Assert.Empty(vm.DisplayedStudents);
            Assert.Equal(2, vm.DisplayedArchivedStudents.Count);

            // 4. ACT - Restore Batch via Confirmation Modal
            vm.IsAllArchiveSelected = true;
            vm.PromptBulkRestoreCommand.Execute(null);
            await vm.ConfirmModal!.ConfirmCommand.ExecuteAsync(null);

            // 5. ASSERT - Restored back to Master Roster
            Assert.Equal(2, vm.DisplayedStudents.Count);
            Assert.Empty(vm.DisplayedArchivedStudents);
        }

        [Fact]
        public async Task BulkDelete_ShouldCascadeDeleteStudent_AndAllChildRecords()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareDirectoryTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            var archivedStudent = TestDataFactory.CreateFakeStudent(studentId: "DEL-01", lastName: "Ghost");
            archivedStudent.IsArchived = true;
            await db.InsertAsync(archivedStudent);

            // Seed relational child records tied to DEL-01
            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "DEL-01" });
            await db.InsertAsync(new Score { AssessmentID = 1, StudentID = "DEL-01", PointsEarned = 95 });
            await db.InsertAsync(new AttendanceRecord { ClassID = 1, StudentID = "DEL-01", Status = "P" });

            var vm = new DirectoryViewModel(fakeDb);
            await vm.LoadStudentsAsync();

            vm.DisplayedArchivedStudents.First().IsSelected = true;

            // 2. ACT
            vm.PromptBulkDeleteCommand.Execute(null);
            await vm.ConfirmModal!.ConfirmCommand.ExecuteAsync(null);

            // 3. ASSERT
            Assert.Empty(vm.DisplayedArchivedStudents);
            Assert.Equal(0, await db.Table<Student>().CountAsync());
            Assert.Equal(0, await db.Table<ClassRoster>().CountAsync());
            Assert.Equal(0, await db.Table<Score>().CountAsync());
            Assert.Equal(0, await db.Table<AttendanceRecord>().CountAsync());
        }

        [Fact]
        public async Task EditStudentId_ShouldCascadeNewId_ToAllChildTables()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareDirectoryTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            await db.InsertAsync(TestDataFactory.CreateFakeStudent(studentId: "TEMP-01", lastName: "Temporary"));
            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "TEMP-01" });
            await db.InsertAsync(new Score { AssessmentID = 1, StudentID = "TEMP-01", PointsEarned = 88 });

            var vm = new DirectoryViewModel(fakeDb);
            await vm.LoadStudentsAsync();
            var row = vm.DisplayedStudents.First();

            // 2. ACT - Step 1: Enter Edit Mode, Step 2: Change ID & Save
            await vm.EditOrSaveStudentCommand.ExecuteAsync(row);
            row.StudentID = "2026-9999";
            await vm.EditOrSaveStudentCommand.ExecuteAsync(row);

            // 3. ASSERT
            Assert.Equal(1, await db.Table<Student>().CountAsync());
            Assert.Equal("2026-9999", (await db.Table<Student>().FirstAsync()).StudentID);
            Assert.Equal("2026-9999", (await db.Table<ClassRoster>().FirstAsync()).StudentID);
            Assert.Equal("2026-9999", (await db.Table<Score>().FirstAsync()).StudentID);
        }
    }
}