using Xunit;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Centriku.Models;
using Centriku.ViewModels;
using Centriku.Tests.Factories;

namespace Centriku.Tests.ViewModels.Gradebook
{
    public class GradebookAttendanceTests
    {
        private async Task PrepareAttendanceTablesAsync(FakeDatabaseService fakeDb)
        {
            var db = fakeDb.GetConnection();
            await db.CreateTablesAsync(SQLite.CreateFlags.None, 
                typeof(AttendanceRecord), typeof(ClassRoster), 
                typeof(TeacherClass), typeof(Student));
        }

        [Fact]
        public async Task SaveRollCallDay_ShouldCreateRecords_ForEnrolledStudents()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareAttendanceTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            // Put two students in the class roster
            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "S-001" });
            await db.InsertAsync(new ClassRoster { ClassID = 1, StudentID = "S-002" });

            var vm = new GradebookViewModel(fakeDb) 
            { 
                ClassId = 1,
                AttendanceDates = new ObservableCollection<DateTime>() 
            };

            // Open the panel and set a date
            vm.IsAddingRollCall = true;
            vm.NewRollCallDate = new DateTime(2026, 10, 4);

            // 2. ACT
            vm.SaveRollCallCommand.Execute(null);

            // SYNCHRONIZATION: SaveRollCallDay is an 'async void' method, 
            // so we give SQLite 100ms to finish the bulk insert before asserting.
            await Task.Delay(100);

            // 3. ASSERT
            var records = await db.Table<AttendanceRecord>().ToListAsync();
            
            // Expect 3 records: 1 GHOST_DATE UI anchor + 2 actual students
            Assert.Equal(3, records.Count);
            
            // Prove default "Present" values were assigned
            Assert.Contains(records, r => r.StudentID == "S-001" && r.Status == "P");
            Assert.Contains(records, r => r.StudentID == "S-002" && r.Status == "P");
            
            // Prove the UI panel closed automatically upon success
            Assert.False(vm.IsAddingRollCall);
        }

        [Fact]
        public async Task SaveRollCallDay_ShouldPreventDuplicateDates_ViaToast()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PrepareAttendanceTablesAsync(fakeDb);

            string toastMessage = "";
            var vm = new GradebookViewModel(fakeDb) 
            { 
                ClassId = 1,
                // Pre-fill the observable collection with an existing date
                AttendanceDates = new ObservableCollection<DateTime> { new DateTime(2026, 10, 5) },
                ShowToastMessage = (msg) => { toastMessage = msg; }
            };

            vm.NewRollCallDate = new DateTime(2026, 10, 5);

            // 2. ACT
            vm.SaveRollCallCommand.Execute(null);
            await Task.Delay(50); 

            // 3. ASSERT
            // Prove the database was untouched and the user was warned
            Assert.Contains("already exists", toastMessage);
            Assert.Empty(await fakeDb.GetConnection().Table<AttendanceRecord>().ToListAsync());
        }
    }
}