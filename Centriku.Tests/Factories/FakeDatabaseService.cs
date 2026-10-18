using SQLite;
using System;
using System.IO;
using System.Threading.Tasks;
using Centriku.Services;
using Centriku.Models;

namespace Centriku.Tests.Factories
{
    // A lightning-fast, highly isolated database strictly for testing
    public class FakeDatabaseService : IDatabaseService
    {
        private readonly SQLiteAsyncConnection _db;
        public string DbPath { get; }

        public FakeDatabaseService()
        {
            // Generates a 100% unique file for every single test so they never collide
            DbPath = Path.Combine(Path.GetTempPath(), $"TestDb_{Guid.NewGuid()}.db");
            _db = new SQLiteAsyncConnection(DbPath);
        }

        public SQLiteAsyncConnection GetConnection() => _db;

        // Helper to quickly prep the tables needed for Settings tests
        public async Task PrepareSettingsTablesAsync()
        {
            await _db.CreateTablesAsync(CreateFlags.None,
                typeof(AppSettings), typeof(Student), typeof(TeacherClass),
                typeof(ClassRoster), typeof(Assessment), typeof(Score),
                typeof(AttendanceRecord));

            // Enforcing Id = 1 ensures we don't trip SQLite auto-increment constraints
            await _db.InsertAsync(new AppSettings 
            { 
                Id = 1, 
                DatabaseSchemaVersion = DatabaseMigrationService.TARGET_SCHEMA_VERSION 
            });
        }
    }
}