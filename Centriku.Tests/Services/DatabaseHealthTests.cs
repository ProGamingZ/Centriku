using Xunit;
using SQLite;
using System;
using System.IO;
using System.Threading.Tasks;
using Centriku.Services;
using Centriku.Models;
using Centriku.Tests.Factories;

namespace Centriku.Tests.Services
{
    public class DatabaseHealthTests
    {
        [Fact]
        public async Task Migrations_ShouldUpgradeSchema_ToTargetVersionSafely()
        {
            // 1. ARRANGE
            string dbPath = Path.Combine(Path.GetTempPath(), $"HealthTest_{Guid.NewGuid()}.db");
            var db = new SQLiteAsyncConnection(dbPath);
            
            // We MUST create the tables first to mimic the real app's boot sequence!
            await db.CreateTablesAsync(CreateFlags.None, 
                typeof(Student), typeof(TeacherClass), typeof(ClassRoster),
                typeof(Assessment), typeof(Score), typeof(ScoreHistory),
                typeof(GradingTemplate), typeof(GradingCategory), typeof(AttendanceRecord),
                typeof(AppSettings), typeof(AssessmentGroup), typeof(AssessmentGroupMember),
                typeof(RecitationQuestion)
            );

            // Seed it as Version 1 so we force the migrator to run all the scripts up to v6
            await db.InsertAsync(new AppSettings { Id = 1, DatabaseSchemaVersion = 1 });

            // Seed legacy broken records (simulating pre-v6 data with 0 hours and old "FDA" drop text)
            await db.InsertAsync(new AttendanceRecord { ClassID = 1, StudentID = "2024-0001", SessionHours = 0 });
            await db.InsertAsync(new TeacherClass { ClassID = 1, SubjectName = "Legacy Class", FdaMaxAbsenceHours = 0, FdaDropGrade = "FDA" });

            var migrator = new DatabaseMigrationService();

            // 2. ACT
            await migrator.RunMigrationsAsync(db);

            // 3. ASSERT
            var settings = await db.Table<AppSettings>().FirstAsync();
            Assert.Equal(DatabaseMigrationService.TARGET_SCHEMA_VERSION, settings.DatabaseSchemaVersion);

            // Prove Migration v6 backfilled the safe defaults!
            var migratedAtt = await db.Table<AttendanceRecord>().FirstAsync();
            var migratedClass = await db.Table<TeacherClass>().FirstAsync();
            Assert.Equal(1.5, migratedAtt.SessionHours);
            Assert.Equal(10.0, migratedClass.FdaMaxAbsenceHours);
            Assert.Equal("5.0", migratedClass.FdaDropGrade);
        }

        [Fact]
        public async Task PurgeOrphanedRecords_ShouldRemoveGhosts_AndProtectValidData()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await fakeDb.PrepareSettingsTablesAsync();
            var db = fakeDb.GetConnection();

            // Use TestDataFactory to seed 1 valid student
            await db.InsertAsync(TestDataFactory.CreateFakeStudent("2024-0001", "Juan", "Dela Cruz"));
            
            // FIX: Capture the inserted class so we use its real AutoIncrement ClassID (1)
            var validClass = new TeacherClass { SubjectName = "CS101" };
            await db.InsertAsync(validClass);

            // 1 Valid Roster + 2 Ghost Rosters (Using validClass.ClassID)
            await db.InsertAsync(new ClassRoster { ClassID = validClass.ClassID, StudentID = "2024-0001" });
            await db.InsertAsync(new ClassRoster { ClassID = validClass.ClassID, StudentID = "GHOST-01" });
            await db.InsertAsync(new ClassRoster { ClassID = validClass.ClassID, StudentID = "GHOST-02" });

            // 1 GHOST_DATE UI anchor (Using validClass.ClassID so it is protected!)
            await db.InsertAsync(new AttendanceRecord { ClassID = validClass.ClassID, StudentID = "GHOST_DATE", Status = "P" });

            var manager = new DatabaseManagementService(fakeDb);

            // 2. ACT
            string scanReport = await manager.ScanOrphanedRecordsAsync();
            int purgedCount = await manager.PurgeOrphanedRecordsAsync();

            // 3. ASSERT
            Assert.Contains("Orphaned Roster Enrollments: 2", scanReport);
            Assert.Equal(2, purgedCount);
            Assert.Equal(1, await db.Table<ClassRoster>().CountAsync());
            Assert.Equal(1, await db.Table<AttendanceRecord>().CountAsync()); // GHOST_DATE survived!
        }

        [Fact]
        public async Task IntegrityCheck_ShouldReturnOk_OnFreshDatabase()
        {
            // 1. ARRANGE
            var fakeDbService = new FakeDatabaseService();
            var manager = new DatabaseManagementService(fakeDbService);

            // 2. ACT
            string result = await manager.RunIntegrityCheckAsync();

            // 3. ASSERT 
            Assert.Equal("ok", result.ToLower());
        }

    }
}