using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels.Settings
{
    public partial class Sf9SettingsViewModel : ViewModelBase
    {
        // === 1. School Identity ===
        [ObservableProperty] public partial string SchoolName { get; set; } = "";
        [ObservableProperty] public partial string SchoolId { get; set; } = "";
        [ObservableProperty] public partial string Region { get; set; } = "";
        [ObservableProperty] public partial string Division { get; set; } = "";
        [ObservableProperty] public partial string District { get; set; } = "";

        // === 2. Signatories ===
        [ObservableProperty] public partial string PrincipalName { get; set; } = "";
        [ObservableProperty] public partial string PrincipalTitle { get; set; } = "Principal I";
        [ObservableProperty] public partial string DefaultTeacherName { get; set; } = "";

        // === 3. Generation & Formatting ===
        [ObservableProperty] public partial string Sf9DefaultExportPath { get; set; } = "";
        [ObservableProperty] public partial string Sf9FileNamingFormat { get; set; } = "[LastName]_[FirstName]_SF9";
        [ObservableProperty] public partial bool Sf9AutoOpenPdf { get; set; } = true;
        
        [ObservableProperty] public partial double PassingGradeThreshold { get; set; } = 75.0;
        partial void OnPassingGradeThresholdChanged(double value) => RefreshPreviews();

        [ObservableProperty] public partial string BlankGradeOutput { get; set; } = "Blank";
        partial void OnBlankGradeOutputChanged(string value) => RefreshPreviews();

        // === 4. Custom Grading Legend ===
        [ObservableProperty] public partial string LegDesc1 { get; set; } = "Outstanding";
        [ObservableProperty] public partial string LegScale1 { get; set; } = "90-100";
        [ObservableProperty] public partial string LegRem1 { get; set; } = "Passed";
        [ObservableProperty] public partial string LegDesc2 { get; set; } = "Very Satisfactory";
        [ObservableProperty] public partial string LegScale2 { get; set; } = "85-89";
        [ObservableProperty] public partial string LegRem2 { get; set; } = "Passed";
        [ObservableProperty] public partial string LegDesc3 { get; set; } = "Satisfactory";
        [ObservableProperty] public partial string LegScale3 { get; set; } = "80-84";
        [ObservableProperty] public partial string LegRem3 { get; set; } = "Passed";
        [ObservableProperty] public partial string LegDesc4 { get; set; } = "Fairly Satisfactory";
        [ObservableProperty] public partial string LegScale4 { get; set; } = "75-79";
        [ObservableProperty] public partial string LegRem4 { get; set; } = "Passed";
        [ObservableProperty] public partial string LegDesc5 { get; set; } = "Did Not Meet Expectations";
        [ObservableProperty] public partial string LegScale5 { get; set; } = "Below 75";
        [ObservableProperty] public partial string LegRem5 { get; set; } = "Failed";

        // === 5. PREVIEW COLLECTIONS ===
        [ObservableProperty] public partial ObservableCollection<Sf9PreviewGradeRow> PreviewGrades { get; set; } = new();

        // === Dropdown Menus ===
        public ObservableCollection<string> NamingFormats { get; } = new() { "[LastName]_[FirstName]_SF9", "[LRN]_SF9", "SF9_[LastName]" };
        public ObservableCollection<string> BlankOutputs { get; } = new() { "Blank", "Dash", "NA" };

        // === Commands & Actions ===
        public IRelayCommand SaveSettingsCommand { get; }
        public IRelayCommand ResetDefaultsCommand { get; }
        public Action<string>? ShowToastMessage { get; set; }

        public Sf9SettingsViewModel()
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
                SchoolName = settings.SchoolName ?? ""; SchoolId = settings.SchoolId ?? "";
                Region = settings.Region ?? ""; Division = settings.Division ?? ""; District = settings.District ?? "";
                PrincipalName = settings.PrincipalName ?? ""; PrincipalTitle = settings.PrincipalTitle ?? "Principal I";
                DefaultTeacherName = settings.DefaultTeacherName ?? "";
                Sf9DefaultExportPath = settings.Sf9DefaultExportPath ?? "";
                Sf9FileNamingFormat = settings.Sf9FileNamingFormat ?? "[LastName]_[FirstName]_SF9";
                Sf9AutoOpenPdf = settings.Sf9AutoOpenPdf;
                PassingGradeThreshold = settings.PassingGradeThreshold;
                BlankGradeOutput = settings.BlankGradeOutput ?? "Blank";

                LegDesc1 = settings.LegDesc1 ?? "Outstanding"; LegScale1 = settings.LegScale1 ?? "90-100"; LegRem1 = settings.LegRem1 ?? "Passed";
                LegDesc2 = settings.LegDesc2 ?? "Very Satisfactory"; LegScale2 = settings.LegScale2 ?? "85-89"; LegRem2 = settings.LegRem2 ?? "Passed";
                LegDesc3 = settings.LegDesc3 ?? "Satisfactory"; LegScale3 = settings.LegScale3 ?? "80-84"; LegRem3 = settings.LegRem3 ?? "Passed";
                LegDesc4 = settings.LegDesc4 ?? "Fairly Satisfactory"; LegScale4 = settings.LegScale4 ?? "75-79"; LegRem4 = settings.LegRem4 ?? "Passed";
                LegDesc5 = settings.LegDesc5 ?? "Did Not Meet Expectations"; LegScale5 = settings.LegScale5 ?? "Below 75"; LegRem5 = settings.LegRem5 ?? "Failed";
            }
            
            RefreshPreviews();
        }

        private async void SaveSettings()
        {
            var db = new DatabaseService().GetConnection();
            var settings = await db.Table<AppSettings>().FirstOrDefaultAsync() ?? new AppSettings();

            settings.SchoolName = this.SchoolName; settings.SchoolId = this.SchoolId;
            settings.Region = this.Region; settings.Division = this.Division; settings.District = this.District;
            settings.PrincipalName = this.PrincipalName; settings.PrincipalTitle = this.PrincipalTitle; settings.DefaultTeacherName = this.DefaultTeacherName;
            settings.Sf9DefaultExportPath = this.Sf9DefaultExportPath; settings.Sf9FileNamingFormat = this.Sf9FileNamingFormat;
            settings.Sf9AutoOpenPdf = this.Sf9AutoOpenPdf; settings.PassingGradeThreshold = this.PassingGradeThreshold; settings.BlankGradeOutput = this.BlankGradeOutput;

            settings.LegDesc1 = this.LegDesc1; settings.LegScale1 = this.LegScale1; settings.LegRem1 = this.LegRem1;
            settings.LegDesc2 = this.LegDesc2; settings.LegScale2 = this.LegScale2; settings.LegRem2 = this.LegRem2;
            settings.LegDesc3 = this.LegDesc3; settings.LegScale3 = this.LegScale3; settings.LegRem3 = this.LegRem3;
            settings.LegDesc4 = this.LegDesc4; settings.LegScale4 = this.LegScale4; settings.LegRem4 = this.LegRem4;
            settings.LegDesc5 = this.LegDesc5; settings.LegScale5 = this.LegScale5; settings.LegRem5 = this.LegRem5;

            if (settings.Id == 0) await db.InsertAsync(settings);
            else await db.UpdateAsync(settings);

            ShowToastMessage?.Invoke("SF9 Settings saved successfully!");
        }

        private void ResetDefaults()
        {
            SchoolName = ""; SchoolId = ""; Region = ""; Division = ""; District = "";
            PrincipalName = ""; PrincipalTitle = "Principal I"; DefaultTeacherName = "";
            Sf9DefaultExportPath = ""; Sf9FileNamingFormat = "[LastName]_[FirstName]_SF9"; Sf9AutoOpenPdf = true;
            PassingGradeThreshold = 75.0; BlankGradeOutput = "Blank";
            
            LegDesc1 = "Outstanding"; LegScale1 = "90-100"; LegRem1 = "Passed";
            LegDesc2 = "Very Satisfactory"; LegScale2 = "85-89"; LegRem2 = "Passed";
            LegDesc3 = "Satisfactory"; LegScale3 = "80-84"; LegRem3 = "Passed";
            LegDesc4 = "Fairly Satisfactory"; LegScale4 = "75-79"; LegRem4 = "Passed";
            LegDesc5 = "Did Not Meet Expectations"; LegScale5 = "Below 75"; LegRem5 = "Failed";

            SaveSettings();
            RefreshPreviews();
        }

        // === THE LIVE PREVIEW ENGINE ===
        private async void RefreshPreviews()
        {
            PreviewGrades.Clear();
            await Task.Delay(50); // Virtualization UI Fix

            var newGrades = new ObservableCollection<Sf9PreviewGradeRow>();

            string FormatGrade(string g)
            {
                if (string.IsNullOrWhiteSpace(g)) return BlankGradeOutput switch { "Dash" => "--", "NA" => "N/A", _ => "" };
                return g;
            }

            string GetRemarks(double final) => final >= PassingGradeThreshold ? "Passed" : "Failed";

            newGrades.Add(new Sf9PreviewGradeRow { Subject = "Mathematics", Q1 = "85", Q2 = "88", Q3 = "87", Q4 = "90", Final = "88", Remarks = GetRemarks(88) });
            newGrades.Add(new Sf9PreviewGradeRow { Subject = "Science", Q1 = "74", Q2 = "75", Q3 = "74", Q4 = "73", Final = "74", Remarks = GetRemarks(74) });
            newGrades.Add(new Sf9PreviewGradeRow { Subject = "English", Q1 = "80", Q2 = "82", Q3 = FormatGrade(""), Q4 = FormatGrade(""), Final = FormatGrade(""), Remarks = FormatGrade("") });

            PreviewGrades = newGrades;
        }
    }

    // Data model for the Preview Grid
    public class Sf9PreviewGradeRow
    {
        public string Subject { get; set; } = "";
        public string Q1 { get; set; } = "";
        public string Q2 { get; set; } = "";
        public string Q3 { get; set; } = "";
        public string Q4 { get; set; } = "";
        public string Final { get; set; } = "";
        public string Remarks { get; set; } = "";
    }
}