using System;
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
        // === 1. User Properties & Auto-Refresh Hooks ===
        [ObservableProperty] public partial string DefaultExportFolderPath { get; set; } = string.Empty;
        [ObservableProperty] public partial string FileNamingFormat { get; set; } = "[Class]_[Term]_[Date]";
        
        [ObservableProperty] public partial bool ExportIncludeLRN { get; set; } = true;
        partial void OnExportIncludeLRNChanged(bool value) => RefreshPreviews();

        [ObservableProperty] public partial bool ExportIncludeArchived { get; set; } = false;
        partial void OnExportIncludeArchivedChanged(bool value) => RefreshPreviews();

        [ObservableProperty] public partial string ExportMissingScoreRule { get; set; } = "Zero";
        partial void OnExportMissingScoreRuleChanged(string value) => RefreshPreviews();

        [ObservableProperty] public partial string ExportDecimalPrecision { get; set; } = "Exact";
        partial void OnExportDecimalPrecisionChanged(string value) => RefreshPreviews();

        [ObservableProperty] public partial string ExportAttendanceDetail { get; set; } = "Detailed";
        partial void OnExportAttendanceDetailChanged(string value) 
        {
            IsAttendanceDetailed = value == "Detailed";
            RefreshPreviews();
        }

        // Toggles the visibility of the daily date columns in the Attendance Preview tab
        [ObservableProperty] public partial bool IsAttendanceDetailed { get; set; } = true;

        // === 2. Preview Collections ===
        [ObservableProperty] public partial ObservableCollection<GradePreviewRow> QuarterlyPreviewRows { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<GradePreviewRow> SemestralPreviewRows { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<AttendancePreviewRow> AttendancePreviewRows { get; set; } = new();

        // === 3. Dropdown Menu Options ===
        public ObservableCollection<string> NamingFormats { get; } = ["[Class]_[Term]_[Date]", "[Date]_[Class]_[Term]"];
        public ObservableCollection<string> MissingScoreRules { get; } = ["Zero", "Blank", "Dash"];
        public ObservableCollection<string> DecimalPrecisions { get; } = ["Exact", "Rounded"];
        public ObservableCollection<string> AttendanceDetails { get; } = ["Detailed", "SummaryOnly"];

        // === 4. Commands & Actions ===
        public IRelayCommand SaveSettingsCommand { get; }
        public IRelayCommand ResetDefaultsCommand { get; }
        public Action<string>? ShowToastMessage { get; set; }

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
            
            // Set initial UI state
            IsAttendanceDetailed = ExportAttendanceDetail == "Detailed";
            RefreshPreviews();
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
            RefreshPreviews();
        }

        // === 5. THE LIVE PREVIEW ENGINE ===
        private async void RefreshPreviews() 
        {

            await Task.Delay(50);
            var newQuarterly = new ObservableCollection<GradePreviewRow>();
            var newSemestral = new ObservableCollection<GradePreviewRow>();
            var newAttendance = new ObservableCollection<AttendancePreviewRow>();

            // Local Helper: Formats numbers based on the user's active dropdown selections
            string FormatScore(double? rawScore)
            {
                if (rawScore == null)
                {
                    return ExportMissingScoreRule switch {
                        "Blank" => "",
                        "Dash" => "--",
                        _ => "0"
                    };
                }
                if (ExportDecimalPrecision == "Rounded") return Math.Round(rawScore.Value, 0).ToString();
                return rawScore.Value.ToString("0.##");
            }

            // A. Populate Quarterly Fake Data
            newQuarterly.Add(new GradePreviewRow { Lrn = "102938475612", LastName = "Dela Cruz", FirstName = "Juan", Score1 = FormatScore(85.5), Score2 = FormatScore(null), Average = FormatScore(42.75) + "%" });
            if (ExportIncludeArchived)
            {
                newQuarterly.Add(new GradePreviewRow { Lrn = "987654321098", LastName = "[ARCHIVED] Rizal", FirstName = "Jose", Score1 = FormatScore(92.4), Score2 = FormatScore(90.1), Average = FormatScore(91.25) + "%" });
            }

            // B. Populate Semestral Fake Data
            newSemestral.Add(new GradePreviewRow { Lrn = "102938475612", LastName = "Dela Cruz", FirstName = "Juan", Score1 = FormatScore(88.8), Score2 = FormatScore(null), Average = FormatScore(44.4) + "%" });
            if (ExportIncludeArchived)
            {
                newSemestral.Add(new GradePreviewRow { Lrn = "987654321098", LastName = "[ARCHIVED] Rizal", FirstName = "Jose", Score1 = FormatScore(90.0), Score2 = FormatScore(85.0), Average = FormatScore(87.5) + "%" });
            }

            // C. Populate Attendance Fake Data
            newAttendance.Add(new AttendancePreviewRow { LastName = "Dela Cruz", FirstName = "Juan", TotalP = 15, TotalL = 2, TotalA = 1, TotalE = 0, Day1 = "P", Day2 = "A" });
            if (ExportIncludeArchived)
            {
                newAttendance.Add(new AttendancePreviewRow { LastName = "[ARCHIVED] Rizal", FirstName = "Jose", TotalP = 18, TotalL = 0, TotalA = 0, TotalE = 0, Day1 = "P", Day2 = "P" });
            }

            QuarterlyPreviewRows = newQuarterly;
            SemestralPreviewRows = newSemestral;
            AttendancePreviewRows = newAttendance;
        }
    }

    // === DATA MODELS FOR THE PREVIEW TABLES ===
    public class GradePreviewRow
    {
        public string Lrn { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string Score1 { get; set; } = string.Empty;
        public string Score2 { get; set; } = string.Empty;
        public string Average { get; set; } = string.Empty;
    }

    public class AttendancePreviewRow
    {
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public int TotalP { get; set; }
        public int TotalL { get; set; }
        public int TotalA { get; set; }
        public int TotalE { get; set; }
        public string Day1 { get; set; } = string.Empty;
        public string Day2 { get; set; } = string.Empty;
    }
}