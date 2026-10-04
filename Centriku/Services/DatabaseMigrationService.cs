using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using Centriku.Models;

namespace Centriku.Services
{
    public class DatabaseMigrationService
    {
        public const int TARGET_SCHEMA_VERSION = 5;

        private readonly Dictionary<int, Func<SQLiteAsyncConnection, Task>> _migrationScripts = new()
        {
            { 2, MigrateToVersion2Async },
            { 3, MigrateToVersion3Async },
            { 4, MigrateToVersion4Async },
            { 5, MigrateToVersion5Async } 
        };

        public async Task RunMigrationsAsync(SQLiteAsyncConnection db)
        {
            var settings = await db.Table<AppSettings>().FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new AppSettings { Id = 1, DatabaseSchemaVersion = 1 };
                await db.InsertAsync(settings);
            }

            int currentVersion = settings.DatabaseSchemaVersion;

            while (currentVersion < TARGET_SCHEMA_VERSION)
            {
                int nextVersion = currentVersion + 1;
                
                try 
                {
                    if (_migrationScripts.TryGetValue(nextVersion, out var migrationScript))
                    {
                        // Execute the script. If SQLite fails, it immediately jumps to the catch block!
                        await migrationScript(db);
                    }

                    currentVersion = nextVersion;
                    settings.DatabaseSchemaVersion = currentVersion;
                    await db.UpdateAsync(settings);
                }
                catch (Exception ex)
                {
                    // LOUD FAILURE: We wrap the raw SQL error in a massive warning and throw it.
                    // Program.cs will catch this and write it directly to crash_log.txt!
                    throw new Exception($"[DATABASE FATAL ERROR] Failed to migrate schema from v{currentVersion} to v{nextVersion}. Reason: {ex.Message}", ex);
                }
            }
        }

        private static async Task MigrateToVersion2Async(SQLiteAsyncConnection db)
        {
            // NO MORE SWALLOWING: If a table or column is missing, let it throw so you can see it in the log.
            await db.ExecuteAsync("UPDATE Assessments SET AssessmentType = 'Solo' WHERE AssessmentType IS NULL;");
            await db.ExecuteAsync("UPDATE Assessments SET GroupWeight = 30.0 WHERE GroupWeight IS NULL OR GroupWeight = 0;");
            await db.ExecuteAsync("UPDATE Assessments SET IndividualWeight = 70.0 WHERE IndividualWeight IS NULL OR IndividualWeight = 0;");
            await db.ExecuteAsync("UPDATE AppSettings SET ProgramColumnIndex = -1 WHERE ProgramColumnIndex IS NULL;");
            await db.ExecuteAsync("UPDATE AppSettings SET SectionNameColumnIndex = -1 WHERE SectionNameColumnIndex IS NULL;");
        }

        private static async Task MigrateToVersion3Async(SQLiteAsyncConnection db)
        {
            var allCategories = await db.Table<GradingCategory>().ToListAsync();
            var groupedByTemplate = allCategories.GroupBy(c => c.TemplateID);
            var categoriesToUpdate = new List<GradingCategory>();
            
            foreach (var group in groupedByTemplate)
            {
                var sortedCategories = group.OrderBy(c => c.SequenceOrder).ToList();
                for (int i = 0; i < sortedCategories.Count; i++)
                {
                    if (sortedCategories[i].SequenceOrder != i)
                    {
                        sortedCategories[i].SequenceOrder = i;
                        categoriesToUpdate.Add(sortedCategories[i]);
                    }
                }
            }
            
            if (categoriesToUpdate.Count > 0)
            {
                await db.UpdateAllAsync(categoriesToUpdate);
            }

            await db.ExecuteAsync("UPDATE GradingCategories SET Name = TRIM(Name);");
            await db.ExecuteAsync("UPDATE Assessments SET Category = TRIM(Category);");
        }
    
        private static async Task MigrateToVersion4Async(SQLiteAsyncConnection db)
        {
            await db.ExecuteAsync("UPDATE GradingCategories SET Name = TRIM(Name);");
            await db.ExecuteAsync("UPDATE Assessments SET Category = TRIM(Category);");
            await db.ExecuteAsync("UPDATE Assessments SET GradingPeriod = TRIM(GradingPeriod);");
            await db.ExecuteAsync("UPDATE Assessments SET Title = TRIM(Title);");
            await db.ExecuteAsync("UPDATE Students SET StudentID = TRIM(StudentID);");
            await db.ExecuteAsync("UPDATE ClassRoster SET StudentID = TRIM(StudentID);");
            await db.ExecuteAsync("UPDATE Scores SET StudentID = TRIM(StudentID);");

            var allCategories = await db.Table<GradingCategory>().ToListAsync();
            var groupedByTemplate = allCategories.GroupBy(c => c.TemplateID);
            var categoriesToUpdate = new List<GradingCategory>();
            
            foreach (var group in groupedByTemplate)
            {
                var sortedCategories = group.OrderBy(c => c.SequenceOrder).ToList();
                for (int i = 0; i < sortedCategories.Count; i++)
                {
                    if (sortedCategories[i].SequenceOrder != i)
                    {
                        sortedCategories[i].SequenceOrder = i;
                        categoriesToUpdate.Add(sortedCategories[i]);
                    }
                }
            }
            if (categoriesToUpdate.Count > 0) await db.UpdateAllAsync(categoriesToUpdate);
        }
    
        private static async Task MigrateToVersion5Async(SQLiteAsyncConnection db)
        {
            try 
            { 
                await db.ExecuteAsync("ALTER TABLE AssessmentGroupMember ADD COLUMN IsLeader INTEGER DEFAULT 0;"); 
            } 
            catch (SQLiteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                // SAFE CATCH: If the column already exists (e.g., from a partial previous run), this is harmless.
                // By filtering for "duplicate column name", any other error (like a corrupted DB) will still crash and log.
            }
        }
    }
}