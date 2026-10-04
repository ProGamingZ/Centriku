using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using Centriku.Models;
using Centriku.ViewModels.Modals;
using Centriku.Tests.Factories;

namespace Centriku.Tests.ViewModels.Modals
{
    public class ModalIntegrationTests
    {
        private async Task PrepareModalTablesAsync(FakeDatabaseService fakeDb)
        {
            var db = fakeDb.GetConnection();
            await db.CreateTablesAsync(SQLite.CreateFlags.None,
                typeof(TeacherClass), typeof(Student), typeof(ClassRoster),
                typeof(Assessment), typeof(AssessmentGroupMember));
        }

        [Fact]
        public async Task RemoveStudentModal_ShouldDeleteRoster_AndWipeGroupMemberships()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareModalTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            var targetStudent = new Student { StudentID = "101", FirstName = "John", LastName = "Doe" };
            await db.InsertAsync(targetStudent);
            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "101" });
            
            // FIX: Let SQLite generate the Assessment, then capture its real ID!
            var testAssessment = new Assessment { ClassID = 1 };
            await db.InsertAsync(testAssessment);
            await db.InsertAsync(new AssessmentGroupMember { AssessmentID = testAssessment.AssessmentID, StudentID = "101", GroupID = 5 });

            bool successCalled = false;
            bool closeCalled = false;
            string toastMessage = "";

            var vm = new RemoveStudentModalViewModel(
                fakeDb,
                classId: 1,
                students: new List<Student> { targetStudent },
                onSuccess: () => { successCalled = true; return Task.CompletedTask; }, 
                onClose: () => { closeCalled = true; },
                showToast: (msg) => { toastMessage = msg; }
            );

            // 2. ACT
            await vm.ConfirmRemoveCommand.ExecuteAsync(null);

            // 3. ASSERT
            var rosters = await db.Table<ClassRoster>().ToListAsync();
            Assert.Empty(rosters); 

            var groupMembers = await db.Table<AssessmentGroupMember>().ToListAsync();
            Assert.Empty(groupMembers); 

            Assert.True(successCalled);
            Assert.True(closeCalled);
            Assert.Contains("Successfully unenrolled", toastMessage);
        }

        [Fact]
        public async Task TransferStudentModal_ShouldUpdateRoster_AndWipeOldGroupMemberships()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareModalTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            var targetStudent = new Student { StudentID = "101", FirstName = "Jane", LastName = "Smith" };
            await db.InsertAsync(targetStudent);

            var sourceClass = new TeacherClass { ClassID = 1, SubjectName = "Math" };
            var destClass = new TeacherClass { ClassID = 2, SubjectName = "Science" };
            await db.InsertAllAsync(new[] { sourceClass, destClass });

            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "101" });
            
            // FIX: Let SQLite generate the Assessment, then capture its real ID!
            var testAssessment = new Assessment { ClassID = 1 };
            await db.InsertAsync(testAssessment);
            await db.InsertAsync(new AssessmentGroupMember { AssessmentID = testAssessment.AssessmentID, StudentID = "101", GroupID = 5 });

            var vm = new TransferStudentModalViewModel(
                fakeDb,
                classId: 1,
                classTitle: "Math",
                students: new List<Student> { targetStudent },
                onSuccess: () => Task.CompletedTask,
                onClose: () => { },
                showToast: (msg) => { }
            );

            await vm.LoadClassesAsync(); 
            vm.SelectedTransferClass = destClass; 

            // 2. ACT
            await vm.ConfirmTransferCommand.ExecuteAsync(null);

            // 3. ASSERT
            var rosters = await db.Table<ClassRoster>().ToListAsync();
            
            Assert.Single(rosters);
            Assert.Equal(2, rosters[0].ClassID); 

            var groupMembers = await db.Table<AssessmentGroupMember>().ToListAsync();
            Assert.Empty(groupMembers); 
        }
    }
}