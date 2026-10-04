using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.ViewModels.Settings;
using Centriku.Services; // Needed for IDatabaseService

namespace Centriku.ViewModels
{
    public partial class SettingsViewModel(IDatabaseService dbService) : ViewModelBase
    {
        [ObservableProperty] public partial int SelectedTabIndex { get; set; } = 0;

        public SystemSettingsViewModel SystemSettings { get; } = new SystemSettingsViewModel(dbService);
        public ImportSettingsViewModel ImportSettings { get; } = new ImportSettingsViewModel();
        public ExportSettingsViewModel ExportSettings { get; } = new ExportSettingsViewModel(dbService);
    }
}