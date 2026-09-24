using System;
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
        [ObservableProperty] public partial string LastBackupDate { get; set; } = "Unknown";
        [ObservableProperty] public partial string DbSizeMb { get; set; } = "0.00 MB";

        [ObservableProperty] public partial string AbsoluteFilePath { get; set; } = string.Empty;
        [ObservableProperty] public partial string DiagnosticOutput { get; set; } = "Awaiting command...\n";

        public SystemSettingsViewModel()
        {
            _dbService = new DatabaseManagementService();
            LoadSystemDiagnostics();
        }

        private void LoadSystemDiagnostics()
        {
            AbsoluteFilePath = _dbService.GetDatabasePath();
            DbSizeMb = $"{_dbService.GetDatabaseSizeMb()} MB";
            
            // Standard health assumption on boot
            DbStatus = "Healthy";
        }

        [RelayCommand]
        public async Task RunIntegrityCheckAsync()
        {
            DiagnosticOutput = "Executing PRAGMA integrity_check...\n";
            string result = await _dbService.RunIntegrityCheckAsync();
            
            DiagnosticOutput += $"\nResult: {result}";
            
            if (result.ToLower() == "ok") DbStatus = "Healthy";
            else DbStatus = "Corrupted!";
        }

        // Called by the Code-Behind after the user chooses a save location
        public async Task ProcessBackupAsync(string destinationFilePath)
        {
            try
            {
                await _dbService.CopyDatabaseToAsync(destinationFilePath);
                LastBackupDate = DateTime.Now.ToString("MMM dd, yyyy");
                DiagnosticOutput = $"Backup successfully saved to:\n{destinationFilePath}";
            }
            catch (Exception ex)
            {
                DiagnosticOutput = $"Backup failed:\n{ex.Message}";
            }
        }

        // Called by the Code-Behind after the user chooses a file to import
        public async Task ProcessRestoreAsync(string sourceFilePath)
        {
            try
            {
                var result = await _dbService.RestoreDatabaseFromAsync(sourceFilePath);
                
                if (result.Success)
                {
                    DiagnosticOutput = "Database restored successfully. A restart is highly recommended.";
                    DbStatus = "Restored (Restart Required)";
                }
                else
                {
                    // This will now print the exact OS or SQL error causing the failure!
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