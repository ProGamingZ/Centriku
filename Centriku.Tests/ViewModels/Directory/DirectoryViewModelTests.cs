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
            await db.CreateTablesAsync(SQLite.CreateFlags.None, typeof(Student), typeof(AppSettings));
        }

        [Fact]
        public async Task SaveStudent_ShouldInsertIntoDatabase_AndRefreshUIList()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareDirectoryTablesAsync(fakeDb);
            
            var vm = new DirectoryViewModel(fakeDb);
            await vm.LoadStudentsAsync(); // Explicitly await the boot sequence

            vm.NewStudentId = "TEST-001";
            vm.NewStudentLastName = "Dela Cruz";
            vm.NewStudentFirstName = "Juan";
            vm.NewStudentProgram = "BSIT";

            // 2. ACT - Use ExecuteAsync!
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
        public async Task SearchQuery_ShouldFilter_DisplayedStudents()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareDirectoryTablesAsync(fakeDb);
            
            var student1 = TestDataFactory.CreateFakeStudent(studentId: "101", lastName: "Smith");
            var student2 = TestDataFactory.CreateFakeStudent(studentId: "102", lastName: "Johnson");
            
            await fakeDb.GetConnection().InsertAllAsync(new[] { student1, student2 });

            var vm = new DirectoryViewModel(fakeDb); 
            await vm.LoadStudentsAsync(); // Explicitly await the boot sequence

            // 2. ACT
            vm.SearchQuery = "smith";

            // 3. ASSERT
            Assert.Single(vm.DisplayedStudents);
            Assert.Equal("Smith", vm.DisplayedStudents.First().LastName);
        }

        [Fact]
        public async Task ArchiveStudent_ShouldMoveStudent_ToArchiveList()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareDirectoryTablesAsync(fakeDb);
            
            var targetStudent = TestDataFactory.CreateFakeStudent(lastName: "ToArchive");
            await fakeDb.GetConnection().InsertAsync(targetStudent);

            var vm = new DirectoryViewModel(fakeDb); 
            await vm.LoadStudentsAsync(); // Explicitly await the boot sequence
            
            var rowToArchive = vm.DisplayedStudents.First();

            // 2. ACT - Use ExecuteAsync!
            await vm.ArchiveStudentCommand.ExecuteAsync(rowToArchive);

            // 3. ASSERT
            Assert.Empty(vm.DisplayedStudents);
            Assert.Single(vm.DisplayedArchivedStudents);
            Assert.Equal("ToArchive", vm.DisplayedArchivedStudents.First().LastName);

            var dbRecord = await fakeDb.GetConnection().Table<Student>().FirstAsync();
            Assert.True(dbRecord.IsArchived);
        }
    }
}