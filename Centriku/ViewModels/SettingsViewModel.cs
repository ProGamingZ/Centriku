using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.ViewModels.Settings;

namespace Centriku.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        [ObservableProperty] public partial int SelectedTabIndex { get; set; } = 0;
        
        public ImportSettingsViewModel ImportSettings { get; } = new();
        public ExportSettingsViewModel ExportSettings { get; } = new();
        public Sf9SettingsViewModel Sf9Settings { get; } = new();
    }
}