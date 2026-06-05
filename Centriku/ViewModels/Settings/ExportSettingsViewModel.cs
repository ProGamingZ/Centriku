using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels.Settings
{
    public partial class ExportSettingsViewModel : ViewModelBase
    {
        // === 1. User Properties ===
        [ObservableProperty] public partial string DefaultExportFolderPath { get; set; } = string.Empty;
        [ObservableProperty] public partial string FileNamingFormat { get; set; } = "[Class]_[Term]_[Date]";
        [ObservableProperty] public partial bool ExportIncludeLRN { get; set; } = true;
        [ObservableProperty] public partial bool ExportIncludeArchived { get; set; } = false;
        [ObservableProperty] public partial string ExportMissingScoreRule { get; set; } = "Zero";
        [ObservableProperty] public partial string ExportDecimalPrecision { get; set; } = "Exact";
        [ObservableProperty] public partial string ExportAttendanceDetail { get; set; } = "Detailed";

        // === 2. Dropdown Menu Options ===
        public ObservableCollection<string> NamingFormats { get; } = ["[Class]_[Term]_[Date]", "[Date]_[Class]_[Term]"];
        public ObservableCollection<string> MissingScoreRules { get; } = ["Zero", "Blank", "Dash"];
        public ObservableCollection<string> DecimalPrecisions { get; } = ["Exact", "Rounded"];
        public ObservableCollection<string> AttendanceDetails { get; } = new() { "Detailed", "SummaryOnly" };

        // === 3. Commands & Actions ===
        public IRelayCommand SaveSettingsCommand { get; }
        public IRelayCommand ResetDefaultsCommand { get; }
        public System.Action<string>? ShowToastMessage { get; set; }

        public ExportSettingsViewModel()
        {
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            ResetDefaultsCommand = new RelayCommand(ResetDefaults);
            LoadSettings();
        }

        private async void LoadSettings()
        {
            var db = new DatabaseService().GetConnection();
            
            await db.CreateTableAsync<AppSettings>();
            var settings = await db.Table<AppSettings>().FirstOrDefaultAsync();

            if (settings != null)
            {
                DefaultExportFolderPath = settings.DefaultExportFolderPath ?? string.Empty;
                FileNamingFormat = settings.FileNamingFormat ?? "[Class]_[Term]_[Date]";
                ExportIncludeLRN = settings.ExportIncludeLRN;
                ExportIncludeArchived = settings.ExportIncludeArchived;
                ExportMissingScoreRule = settings.ExportMissingScoreRule ?? "Zero";
                ExportDecimalPrecision = settings.ExportDecimalPrecision ?? "Exact";
                ExportAttendanceDetail = settings.ExportAttendanceDetail ?? "Detailed";
            }
        }

        private async void SaveSettings()
        {
            var db = new DatabaseService().GetConnection();
            var settings = await db.Table<AppSettings>().FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new AppSettings();
                await db.InsertAsync(settings);
            }

            settings.DefaultExportFolderPath = this.DefaultExportFolderPath;
            settings.FileNamingFormat = this.FileNamingFormat;
            settings.ExportIncludeLRN = this.ExportIncludeLRN;
            settings.ExportIncludeArchived = this.ExportIncludeArchived;
            settings.ExportMissingScoreRule = this.ExportMissingScoreRule;
            settings.ExportDecimalPrecision = this.ExportDecimalPrecision;
            settings.ExportAttendanceDetail = this.ExportAttendanceDetail;

            await db.UpdateAsync(settings);
            
            ShowToastMessage?.Invoke("Export settings saved successfully!");
        }

        private void ResetDefaults()
        {
            DefaultExportFolderPath = string.Empty;
            FileNamingFormat = "[Class]_[Term]_[Date]";
            ExportIncludeLRN = true;
            ExportIncludeArchived = false;
            ExportMissingScoreRule = "Zero";
            ExportDecimalPrecision = "Exact";
            ExportAttendanceDetail = "Detailed";
            
            SaveSettings();
        }
    }
}