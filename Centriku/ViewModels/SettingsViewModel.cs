using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;

namespace Centriku.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        [ObservableProperty] public partial AppSettings CurrentSettings { get; set; } = new();
        [ObservableProperty] public partial bool IsSaving { get; set; } = false;

        // The Dropdown Options
        public ObservableCollection<string> AvailableColumns { get; } = new()
        {
            "Ignore (Skip)", "Column A (1)", "Column B (2)", "Column C (3)", "Column D (4)", 
            "Column E (5)", "Column F (6)", "Column G (7)", "Column H (8)", "Column I (9)", "Column J (10)"
        };
        public ObservableCollection<string> AvailableGenders { get; } = new() { "Unspecified", "Male", "Female" };
        public ObservableCollection<string> AvailableStatuses { get; } = new() { "Regular", "Irregular", "Transferee" };

        // Translators: These sync the UI Dropdown Index with the Database (-1 offset)
        public int LrnSelectedIndex { get => CurrentSettings.LrnColumnIndex + 1; set { CurrentSettings.LrnColumnIndex = value - 1; OnPropertyChanged(); } }
        public int LastNameSelectedIndex { get => CurrentSettings.LastNameColumnIndex + 1; set { CurrentSettings.LastNameColumnIndex = value - 1; OnPropertyChanged(); } }
        public int FirstNameSelectedIndex { get => CurrentSettings.FirstNameColumnIndex + 1; set { CurrentSettings.FirstNameColumnIndex = value - 1; OnPropertyChanged(); } }
        public int MiddleNameSelectedIndex { get => CurrentSettings.MiddleNameColumnIndex + 1; set { CurrentSettings.MiddleNameColumnIndex = value - 1; OnPropertyChanged(); } }
        public int GenderSelectedIndex { get => CurrentSettings.GenderColumnIndex + 1; set { CurrentSettings.GenderColumnIndex = value - 1; OnPropertyChanged(); } }
        public int GradeSelectedIndex { get => CurrentSettings.GradeLevelColumnIndex + 1; set { CurrentSettings.GradeLevelColumnIndex = value - 1; OnPropertyChanged(); } }
        public int SectionSelectedIndex { get => CurrentSettings.SectionColumnIndex + 1; set { CurrentSettings.SectionColumnIndex = value - 1; OnPropertyChanged(); } }
        public int StatusSelectedIndex { get => CurrentSettings.EnrollmentStatusColumnIndex + 1; set { CurrentSettings.EnrollmentStatusColumnIndex = value - 1; OnPropertyChanged(); } }

        public IRelayCommand SaveSettingsCommand { get; }

        public SettingsViewModel()
        {
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            LoadSettings();
        }

        private async void LoadSettings()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.CreateTableAsync<AppSettings>(); // Ensure table exists!

            var savedSettings = await db.Table<AppSettings>().FirstOrDefaultAsync();
            if (savedSettings != null)
            {
                CurrentSettings = savedSettings;
            }
            else
            {
                CurrentSettings = new AppSettings(); // Use the defaults we created in Phase 1
                await db.InsertAsync(CurrentSettings);
            }

            RefreshDropdowns();
        }

        private async void SaveSettings()
        {
            IsSaving = true;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.UpdateAsync(CurrentSettings);
            
            await Task.Delay(600); // Tiny delay so the user sees the "Saving..." feedback!
            IsSaving = false;
        }

        private void RefreshDropdowns()
        {
            OnPropertyChanged(nameof(LrnSelectedIndex)); OnPropertyChanged(nameof(LastNameSelectedIndex));
            OnPropertyChanged(nameof(FirstNameSelectedIndex)); OnPropertyChanged(nameof(MiddleNameSelectedIndex));
            OnPropertyChanged(nameof(GenderSelectedIndex)); OnPropertyChanged(nameof(GradeSelectedIndex));
            OnPropertyChanged(nameof(SectionSelectedIndex)); OnPropertyChanged(nameof(StatusSelectedIndex));
            OnPropertyChanged(nameof(CurrentSettings));
        }
    }
}