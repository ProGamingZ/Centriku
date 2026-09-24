using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using Centriku.Models;

namespace Centriku.Services
{
    public class DatabaseService
    {
        private static SQLiteAsyncConnection? _database;
        
        // 1. The Traffic Light
        private static readonly TaskCompletionSource<bool> _dbReadySignal = new TaskCompletionSource<bool>();
        
        // 2. THE PADLOCK: Guarantees the database can only be initialized by one thread at a time
        private static readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

        public DatabaseService()
        {
            if (_database == null)
            {
                string dbPath = StorageService.GetDatabasePath();
                _database = new SQLiteAsyncConnection(dbPath);
            }
        }

        public async Task InitializeDatabaseAsync()
        {
            // If already built, skip entirely
            if (_dbReadySignal.Task.IsCompleted) return;

            // Lock the door! Any other threads trying to initialize will freeze here and wait.
            await _initLock.WaitAsync();
            try
            {
                // Double-check: If another thread just finished building it while we were waiting, skip.
                if (_dbReadySignal.Task.IsCompleted) return;

                // Atomic Table Creation (Plural)
                await _database!.CreateTablesAsync(CreateFlags.None, 
                    typeof(Student), typeof(TeacherClass), typeof(ClassRoster),
                    typeof(Assessment), typeof(Score), typeof(ScoreHistory),
                    typeof(GradingTemplate), typeof(GradingCategory), typeof(AttendanceRecord),
                    typeof(AppSettings), typeof(AssessmentGroup), typeof(AssessmentGroupMember),
                    typeof(RecitationQuestion)
                );
                
                var migrator = new DatabaseMigrationService();
                await migrator.RunMigrationsAsync(_database);

                // Turn the traffic light green!
                _dbReadySignal.TrySetResult(true);
            }
            finally
            {
                // Unlock the door
                _initLock.Release();
            }
        }
        
        public SQLiteAsyncConnection GetConnection() => _database!;

        // ViewModels call this to wait safely at the traffic light
        public static Task WaitForDatabaseReadyAsync() => _dbReadySignal.Task;
    }
}