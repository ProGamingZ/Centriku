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
        // 1. Force execution by bumping the Target Version to 4
        public const int TARGET_SCHEMA_VERSION = 4;

        private readonly Dictionary<int, Func<SQLiteAsyncConnection, Task>> _migrationScripts = new()
        {
            { 2, MigrateToVersion2Async },
            { 3, MigrateToVersion3Async },
            { 4, MigrateToVersion4Async } // Add V4
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
                
                if (_migrationScripts.TryGetValue(nextVersion, out var migrationScript))
                {
                    await migrationScript(db);
                }

                currentVersion = nextVersion;
                settings.DatabaseSchemaVersion = currentVersion;
                await db.UpdateAsync(settings);
            }
        }

        private static async Task MigrateToVersion2Async(SQLiteAsyncConnection db)
        {
            try { await db.ExecuteAsync("UPDATE Assessments SET AssessmentType = 'Solo' WHERE AssessmentType IS NULL;"); } catch { }
            try { await db.ExecuteAsync("UPDATE Assessments SET GroupWeight = 30.0 WHERE GroupWeight IS NULL OR GroupWeight = 0;"); } catch { }
            try { await db.ExecuteAsync("UPDATE Assessments SET IndividualWeight = 70.0 WHERE IndividualWeight IS NULL OR IndividualWeight = 0;"); } catch { }
            try { await db.ExecuteAsync("UPDATE AppSettings SET ProgramColumnIndex = -1 WHERE ProgramColumnIndex IS NULL;"); } catch { }
            try { await db.ExecuteAsync("UPDATE AppSettings SET SectionNameColumnIndex = -1 WHERE SectionNameColumnIndex IS NULL;"); } catch { }
        }

        // THE V3 AUTO-HEALER
        private static async Task MigrateToVersion3Async(SQLiteAsyncConnection db)
        {
            // The old database analysis proved SequenceOrders were saved as 1, 2, 3 instead of 0, 1, 2.
            // We must rewrite them to be strictly 0-based for the new Math Engine array.
            try 
            {
                var allCategories = await db.Table<GradingCategory>().ToListAsync();
                var groupedByTemplate = allCategories.GroupBy(c => c.TemplateID);
                
                var categoriesToUpdate = new List<GradingCategory>();
                
                foreach (var group in groupedByTemplate)
                {
                    // Sort them by their old broken numbers
                    var sortedCategories = group.OrderBy(c => c.SequenceOrder).ToList();
                    
                    // Rewrite them mathematically starting from 0
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
            } 
            catch { }

            // Trim hidden spaces to ensure names match perfectly
            try { await db.ExecuteAsync("UPDATE GradingCategories SET Name = TRIM(Name);"); } catch { }
            try { await db.ExecuteAsync("UPDATE Assessments SET Category = TRIM(Category);"); } catch { }
        }
    
        // THE VERSION 4 FORCE-HEALER
        private static async Task MigrateToVersion4Async(SQLiteAsyncConnection db)
        {
            // 1. Violently trim all invisible spaces (e.g. "Major Exam " -> "Major Exam")
            // This guarantees the math engine can match the old quizzes to the categories
            try { await db.ExecuteAsync("UPDATE GradingCategories SET Name = TRIM(Name);"); } catch { }
            try { await db.ExecuteAsync("UPDATE Assessments SET Category = TRIM(Category);"); } catch { }
            try { await db.ExecuteAsync("UPDATE Assessments SET GradingPeriod = TRIM(GradingPeriod);"); } catch { }
            try { await db.ExecuteAsync("UPDATE Assessments SET Title = TRIM(Title);"); } catch { }
            try { await db.ExecuteAsync("UPDATE Students SET StudentID = TRIM(StudentID);"); } catch { }
            try { await db.ExecuteAsync("UPDATE ClassRoster SET StudentID = TRIM(StudentID);"); } catch { }
            try { await db.ExecuteAsync("UPDATE Scores SET StudentID = TRIM(StudentID);"); } catch { }

            // 2. Re-enforce strict 0, 1, 2 array bounds. If an old class had gaps (e.g. 1, 2, 4), 
            // this squashes them sequentially so Avalonia UI can bind to them properly.
            try 
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
                
                if (categoriesToUpdate.Count > 0) await db.UpdateAllAsync(categoriesToUpdate);
            } 
            catch { }
        }
    }
}