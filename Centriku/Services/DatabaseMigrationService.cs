using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SQLite;
using Centriku.Models;

namespace Centriku.Services
{
    public class DatabaseMigrationService
    {
        // 1. The Target Version: Update this number whenever you release a major update with new DB columns
        public const int TARGET_SCHEMA_VERSION = 2;

        // 2. The Dynamic Registry: Maps target versions to their specific data-backfill scripts
        private readonly Dictionary<int, Func<SQLiteAsyncConnection, Task>> _migrationScripts = new()
        {
            { 2, MigrateToVersion2Async },
            // Future updates will just be added here: 
            // { 3, MigrateToVersion3Async },
            // { 4, MigrateToVersion4Async }
        };

        public async Task RunMigrationsAsync(SQLiteAsyncConnection db)
        {
            var settings = await db.Table<AppSettings>().FirstOrDefaultAsync();
            if (settings == null) return; // App is brand new, no historical data to migrate

            int currentVersion = settings.DatabaseSchemaVersion;

            // 3. The Dynamic Engine: Automatically loops through any missing versions sequentially
            // If someone imports a Version 1 file into a Version 4 app, this runs V2, then V3, then V4 automatically.
            while (currentVersion < TARGET_SCHEMA_VERSION)
            {
                int nextVersion = currentVersion + 1;
                
                // If a specific script exists for this version jump, run it
                if (_migrationScripts.TryGetValue(nextVersion, out var migrationScript))
                {
                    await migrationScript(db);
                }

                // Increment the tracker and save it, ensuring this script never runs twice
                currentVersion = nextVersion;
                settings.DatabaseSchemaVersion = currentVersion;
                await db.UpdateAsync(settings);
            }
        }

        // ==========================================
        // EXPLICIT DATA BACKFILL SCRIPTS
        // ==========================================

        private static async Task MigrateToVersion2Async(SQLiteAsyncConnection db)
        {
            // V2 brought Groups and Recitation. We must backfill default math and text for V1 records.
            try { await db.ExecuteAsync("UPDATE Assessments SET AssessmentType = 'Solo' WHERE AssessmentType IS NULL;"); } catch { }
            try { await db.ExecuteAsync("UPDATE Assessments SET GroupWeight = 30.0 WHERE GroupWeight IS NULL OR GroupWeight = 0;"); } catch { }
            try { await db.ExecuteAsync("UPDATE Assessments SET IndividualWeight = 70.0 WHERE IndividualWeight IS NULL OR IndividualWeight = 0;"); } catch { }
            
            try { await db.ExecuteAsync("UPDATE AppSettings SET ProgramColumnIndex = -1 WHERE ProgramColumnIndex IS NULL;"); } catch { }
            try { await db.ExecuteAsync("UPDATE AppSettings SET SectionNameColumnIndex = -1 WHERE SectionNameColumnIndex IS NULL;"); } catch { }
        }

        // Future script placeholder:
        // private static async Task MigrateToVersion3Async(SQLiteAsyncConnection db) { ... }
    }
}