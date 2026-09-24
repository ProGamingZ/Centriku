using System;
using System.IO;
using System.Threading.Tasks;

namespace Centriku.Services
{
    public class DatabaseManagementService
    {
        // Path logic assumes standard LocalApplicationData deployment. Adjust if your DatabaseService saves it elsewhere.
        private readonly string _dbPath;
        private readonly DatabaseService _baseDb;

        public DatabaseManagementService()
        {
            _baseDb = new DatabaseService();
            
            // Dynamically gets the exact folder where your app is running (e.g., your Desktop folder)
            string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
            
            // Points to the exact Centriku_Data folder you saw in your File Explorer
            string dataFolder = Path.Combine(baseDir, "Centriku_Data");
            _dbPath = Path.Combine(dataFolder, "centriku.db");

            // Safety check: Ensures the folder exists before Windows tries to copy files into it
            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }
        }

        public string GetDatabasePath() => _dbPath;

        public double GetDatabaseSizeMb()
        {
            if (File.Exists(_dbPath))
            {
                var info = new FileInfo(_dbPath);
                return Math.Round(info.Length / 1048576.0, 2); // Convert Bytes to MB
            }
            return 0;
        }

        public async Task<string> RunIntegrityCheckAsync()
        {
            try
            {
                var db = _baseDb.GetConnection();
                // SQLite PRAGMA integrity_check returns "ok" if there is no corruption
                var result = await db.ExecuteScalarAsync<string>("PRAGMA integrity_check;");
                return result ?? "Error: No result returned.";
            }
            catch (Exception ex)
            {
                return $"Corruption detected or PRAGMA error:\n{ex.Message}";
            }
        }

        public async Task VacuumDatabaseAsync()
        {
            var db = _baseDb.GetConnection();
            await db.ExecuteAsync("VACUUM;");
        }

        public async Task CopyDatabaseToAsync(string destinationPath)
        {
            // 1. Clean and optimize the database before exporting
            await VacuumDatabaseAsync();
            
            // 2. Close the active connection to release the file lock
            await _baseDb.GetConnection().CloseAsync(); 
            
            // 3. Copy the raw file
            File.Copy(_dbPath, destinationPath, overwrite: true);
        }

        public async Task<(bool Success, string ErrorMessage)> RestoreDatabaseFromAsync(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath)) return (false, "Source file does not exist.");

                // 1. Safely close current connection to release the file lock
                await _baseDb.GetConnection().CloseAsync();

                // 2. Force garbage collection to clean up unmanaged SQLite resources
                GC.Collect();
                GC.WaitForPendingFinalizers();

               // Give the Operating System time to physically release the file handle
                await Task.Delay(500);

                // 3. THE FAIL-SAFE PRINCIPLE: Create a silent backup of the CURRENT database before overwriting
                string failSafePath = _dbPath + ".failsafe.bak";
                File.Copy(_dbPath, failSafePath, overwrite: true);

                // 4. Overwrite the database with the imported file
                File.Copy(sourcePath, _dbPath, overwrite: true);
                
                // If we do not delete the WAL and SHM files, SQLite will attempt to merge 
                // the newer log files into the older database on the next boot, causing a fatal memory crash.
                string walPath = _dbPath + "-wal";
                string shmPath = _dbPath + "-shm";
                if (File.Exists(walPath)) File.Delete(walPath);
                if (File.Exists(shmPath)) File.Delete(shmPath);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                // If the file was corrupted, locked, or migration failed, revert to the fail-safe
                string failSafePath = _dbPath + ".failsafe.bak";
                if (File.Exists(failSafePath))
                {
                    try { File.Copy(failSafePath, _dbPath, overwrite: true); } catch { /* Ignore secondary revert errors */ }
                }
                
                // Return the EXACT error message so we can see it in the UI
                return (false, ex.Message);
            }
        }
    }
}