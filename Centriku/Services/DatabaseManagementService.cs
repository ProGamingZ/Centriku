using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centriku.Models;

namespace Centriku.Services
{
    public class DatabaseManagementService
    {
        private readonly string _dbPath;
        private readonly IDatabaseService _baseDb;

        public DatabaseManagementService(IDatabaseService dbService)
        {
            _baseDb = dbService;
            // SINGLE SOURCE OF TRUTH: Delegates path resolution to StorageService
            _dbPath = StorageService.GetDatabasePath();
        }

        public string GetDatabasePath() => _dbPath;

        public double GetDatabaseSizeMb()
        {
            if (File.Exists(_dbPath))
            {
                var info = new FileInfo(_dbPath);
                return Math.Round(info.Length / 1048576.0, 2);
            }
            return 0;
        }

        public async Task<(int SchemaVersion, string RowCountsSummary)> GetSystemTelemetryAsync()
        {
            var db = _baseDb.GetConnection();
            var settings = await db.Table<AppSettings>().FirstOrDefaultAsync();
            int version = settings?.DatabaseSchemaVersion ?? 1;

            int students = await db.Table<Student>().CountAsync();
            int classes = await db.Table<TeacherClass>().CountAsync();
            int rosters = await db.Table<ClassRoster>().CountAsync();
            int assessments = await db.Table<Assessment>().CountAsync();
            int scores = await db.Table<Score>().CountAsync();
            int attendance = await db.Table<AttendanceRecord>().CountAsync();

            string summary = $"Students: {students} | Classes: {classes} | Rosters: {rosters} | Assessments: {assessments} | Scores: {scores} | Attendance: {attendance}";
            return (version, summary);
        }

        public async Task<string> RunIntegrityCheckAsync()
        {
            try
            {
                var db = _baseDb.GetConnection();
                var result = await db.ExecuteScalarAsync<string>("PRAGMA integrity_check;");
                return result ?? "Error: No result returned.";
            }
            catch (Exception ex)
            {
                return $"Corruption detected or PRAGMA error:\n{ex.Message}";
            }
        }

        public async Task<string> ScanOrphanedRecordsAsync()
        {
            var db = _baseDb.GetConnection();
            var validStudentIds = (await db.Table<Student>().ToListAsync()).Select(s => s.StudentID).ToHashSet();
            var validClassIds = (await db.Table<TeacherClass>().ToListAsync()).Select(c => c.ClassID).ToHashSet();
            var validAssessmentIds = (await db.Table<Assessment>().ToListAsync()).Select(a => a.AssessmentID).ToHashSet();

            var rosters = await db.Table<ClassRoster>().ToListAsync();
            var scores = await db.Table<Score>().ToListAsync();
            var attendance = await db.Table<AttendanceRecord>().ToListAsync();

            var orphanRosters = rosters.Where(r => !validStudentIds.Contains(r.StudentID) || !validClassIds.Contains(r.ClassID)).ToList();
            var orphanScores = scores.Where(s => !validStudentIds.Contains(s.StudentID) || !validAssessmentIds.Contains(s.AssessmentID)).ToList();
            var orphanAttendance = attendance.Where(a => a.StudentID != "GHOST_DATE" && (!validStudentIds.Contains(a.StudentID!) || !validClassIds.Contains(a.ClassID))).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"[RELATIONAL AUDIT COMPLETE - {DateTime.Now:HH:mm:ss}]");
            sb.AppendLine($"• Orphaned Roster Enrollments: {orphanRosters.Count}");
            if (orphanRosters.Count > 0)
                sb.AppendLine($"  -> Ghost Student IDs in Rosters: {string.Join(", ", orphanRosters.Select(r => r.StudentID).Distinct())}");

            sb.AppendLine($"• Orphaned Score Records: {orphanScores.Count}");
            sb.AppendLine($"• Orphaned Attendance Records: {orphanAttendance.Count}");

            int total = orphanRosters.Count + orphanScores.Count + orphanAttendance.Count;
            sb.AppendLine(total == 0
                ? "\nStatus: 100% Clean. All foreign keys match valid records."
                : $"\nStatus: Found {total} ghost record(s). Click 'Purge Ghost Records' to clean.");

            return sb.ToString();
        }

        public async Task<int> PurgeOrphanedRecordsAsync()
        {
            var db = _baseDb.GetConnection();
            var validStudentIds = (await db.Table<Student>().ToListAsync()).Select(s => s.StudentID).ToHashSet();
            var validClassIds = (await db.Table<TeacherClass>().ToListAsync()).Select(c => c.ClassID).ToHashSet();
            var validAssessmentIds = (await db.Table<Assessment>().ToListAsync()).Select(a => a.AssessmentID).ToHashSet();

            var rosters = await db.Table<ClassRoster>().ToListAsync();
            var scores = await db.Table<Score>().ToListAsync();
            var attendance = await db.Table<AttendanceRecord>().ToListAsync();

            var orphanRosters = rosters.Where(r => !validStudentIds.Contains(r.StudentID) || !validClassIds.Contains(r.ClassID)).ToList();
            var orphanScores = scores.Where(s => !validStudentIds.Contains(s.StudentID) || !validAssessmentIds.Contains(s.AssessmentID)).ToList();
            var orphanAttendance = attendance.Where(a => a.StudentID != "GHOST_DATE" && (!validStudentIds.Contains(a.StudentID!) || !validClassIds.Contains(a.ClassID))).ToList();

            int totalPurged = orphanRosters.Count + orphanScores.Count + orphanAttendance.Count;

            if (totalPurged > 0)
            {
                await db.RunInTransactionAsync(tran =>
                {
                    foreach (var r in orphanRosters) tran.Delete(r);
                    foreach (var s in orphanScores) tran.Delete(s);
                    foreach (var a in orphanAttendance) tran.Delete(a);
                });
            }

            await VacuumDatabaseAsync();
            return totalPurged;
        }

        public async Task VacuumDatabaseAsync()
        {
            var db = _baseDb.GetConnection();
            await db.ExecuteAsync("VACUUM;");
        }

        public async Task CopyDatabaseToAsync(string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            // HOT BACKUP PRINCIPLE: 'VACUUM INTO' safely creates a defragmented copy 
            // while the database connection remains live and open!
            var db = _baseDb.GetConnection();
            string escapedPath = destinationPath.Replace("'", "''");
            await db.ExecuteAsync($"VACUUM INTO '{escapedPath}';");
        }

        public async Task<(bool Success, string ErrorMessage)> RestoreDatabaseFromAsync(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath)) return (false, "Source file does not exist.");

                await _baseDb.GetConnection().CloseAsync();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(500);

                string failSafePath = _dbPath + ".failsafe.bak";
                File.Copy(_dbPath, failSafePath, overwrite: true);
                File.Copy(sourcePath, _dbPath, overwrite: true);

                string walPath = _dbPath + "-wal";
                string shmPath = _dbPath + "-shm";
                if (File.Exists(walPath)) File.Delete(walPath);
                if (File.Exists(shmPath)) File.Delete(shmPath);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                string failSafePath = _dbPath + ".failsafe.bak";
                if (File.Exists(failSafePath))
                {
                    try { File.Copy(failSafePath, _dbPath, overwrite: true); } catch { }
                }
                return (false, ex.Message);
            }
        }
    }
}