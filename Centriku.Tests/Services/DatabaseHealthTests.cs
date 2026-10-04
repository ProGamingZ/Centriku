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

            // Seed it as Version 1 so we force the migrator to run all the scripts up to v5
            await db.InsertAsync(new AppSettings { Id = 1, DatabaseSchemaVersion = 1 });

            var migrator = new DatabaseMigrationService();

            // 2. ACT
            await migrator.RunMigrationsAsync(db);

            // 3. ASSERT
            var settings = await db.Table<AppSettings>().FirstAsync();
            Assert.Equal(DatabaseMigrationService.TARGET_SCHEMA_VERSION, settings.DatabaseSchemaVersion);
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