using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Services;

namespace Centriku.ViewModels.Settings
{
    public partial class SystemSettingsViewModel : ViewModelBase
    {
        private readonly DatabaseManagementService _dbService;

        [ObservableProperty] public partial bool IsDeveloperMode { get; set; } = false;

        [ObservableProperty] public partial string DbStatus { get; set; } = "Checking...";
        [ObservableProperty] public partial string LastBackupDate { get; set; } = "Never (Session)";
        [ObservableProperty] public partial string DbSizeMb { get; set; } = "0.00 MB";
        [ObservableProperty] public partial string SchemaVersionDisplay { get; set; } = "v--";
        [ObservableProperty] public partial string TelemetrySummary { get; set; } = "Loading table metrics...";
        [ObservableProperty] public partial string StandardFeedbackMessage { get; set; } = string.Empty;

        [ObservableProperty] public partial string AbsoluteFilePath { get; set; } = string.Empty;
        [ObservableProperty] public partial string DiagnosticOutput { get; set; } = "Awaiting command...\n";

        public SystemSettingsViewModel(IDatabaseService dbService)
        {
            _dbService = new DatabaseManagementService(dbService);
            _ = LoadSystemDiagnosticsAsync();
        }

        public async Task LoadSystemDiagnosticsAsync()
        {
            AbsoluteFilePath = _dbService.GetDatabasePath();
            DbSizeMb = $"{_dbService.GetDatabaseSizeMb()} MB";
            DbStatus = "Healthy";

            var telemetry = await _dbService.GetSystemTelemetryAsync();
            SchemaVersionDisplay = $"v{telemetry.SchemaVersion} (Target: v{DatabaseMigrationService.TARGET_SCHEMA_VERSION})";
            TelemetrySummary = telemetry.RowCountsSummary;
        }

        // STANDARD MODE COMMAND: Friendly 1-Click Maintenance for Teachers
        [RelayCommand]
        public async Task OptimizeSystemAsync()
        {
            try
            {
                DbStatus = "Optimizing...";
                int purged = await _dbService.PurgeOrphanedRecordsAsync();
                string integrity = await _dbService.RunIntegrityCheckAsync();
                await LoadSystemDiagnosticsAsync();

                DbStatus = integrity.ToLower() == "ok" ? "Healthy" : "Attention Needed";
                StandardFeedbackMessage = purged > 0
                    ? $"Optimization complete! Cleaned {purged} unused ghost record(s) and compacted storage."
                    : "Optimization complete! Database is 100% clean and compacted.";
            }
            catch (Exception ex)
            {
                DbStatus = "Error";
                StandardFeedbackMessage = $"Optimization failed: {ex.Message}";
            }
        }

        // DEVELOPER COMMAND 1: Physical Integrity
        [RelayCommand]
        public async Task RunIntegrityCheckAsync()
        {
            DiagnosticOutput = $"[{DateTime.Now:HH:mm:ss}] Executing PRAGMA integrity_check;...\n";
            string result = await _dbService.RunIntegrityCheckAsync();
            DiagnosticOutput += $"Result: {result}\n";
            DbStatus = result.ToLower() == "ok" ? "Healthy" : "Corrupted!";
        }

        // DEVELOPER COMMAND 2: Relational Orphan Audit
        [RelayCommand]
        public async Task ScanOrphansAsync()
        {
            DiagnosticOutput = await _dbService.ScanOrphanedRecordsAsync();
        }

        // DEVELOPER COMMAND 3: Purge Ghost Data
        [RelayCommand]
        public async Task PurgeOrphansAsync()
        {
            int count = await _dbService.PurgeOrphanedRecordsAsync();
            await LoadSystemDiagnosticsAsync();
            DiagnosticOutput = $"[{DateTime.Now:HH:mm:ss}] Executed Relational Purge & VACUUM.\nRemoved {count} orphaned record(s).\nNew Table Counts -> {TelemetrySummary}";
        }

        // DEVELOPER COMMAND 4: Reclaim Disk Space
        [RelayCommand]
        public async Task RunVacuumAsync()
        {
            await _dbService.VacuumDatabaseAsync();
            await LoadSystemDiagnosticsAsync();
            DiagnosticOutput = $"[{DateTime.Now:HH:mm:ss}] Executed SQLite VACUUM; successfully.\nCurrent Database Size: {DbSizeMb}";
        }

        // DEVELOPER COMMAND 5: Open OS Folder
        [RelayCommand]
        public void OpenDataFolder()
        {
            try
            {
                string folder = StorageService.GetAppFolderPath();
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
                DiagnosticOutput = $"[{DateTime.Now:HH:mm:ss}] Opened data directory:\n{folder}";
            }
            catch (Exception ex)
            {
                DiagnosticOutput = $"Failed to open folder: {ex.Message}";
            }
        }

        public async Task ProcessBackupAsync(string destinationFilePath)
        {
            try
            {
                await _dbService.CopyDatabaseToAsync(destinationFilePath);
                LastBackupDate = DateTime.Now.ToString("MMM dd, yyyy HH:mm");
                StandardFeedbackMessage = $"Backup saved successfully to {destinationFilePath}";
                DiagnosticOutput = $"Backup successfully saved to:\n{destinationFilePath}";
            }
            catch (Exception ex)
            {
                StandardFeedbackMessage = $"Backup failed: {ex.Message}";
                DiagnosticOutput = $"Backup failed:\n{ex.Message}";
            }
        }

        public async Task ProcessRestoreAsync(string sourceFilePath)
        {
            try
            {
                var result = await _dbService.RestoreDatabaseFromAsync(sourceFilePath);
                if (result.Success)
                {
                    DbStatus = "Restored (Restart Required)";
                    StandardFeedbackMessage = "Database restored! Please restart Centriku to load your data.";
                    DiagnosticOutput = "Database restored successfully. A restart is highly recommended.";
                }
                else
                {
                    StandardFeedbackMessage = $"Restore failed: {result.ErrorMessage}";
                    DiagnosticOutput = $"Failed to overwrite the database. Your current data is safe.\n\nERROR DETAILS:\n{result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                DiagnosticOutput = $"Restore failed fatally:\n{ex.Message}";
            }
        }
    }
}