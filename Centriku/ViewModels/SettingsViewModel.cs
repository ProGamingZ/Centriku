using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.ViewModels.Settings;

namespace Centriku.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        [ObservableProperty] public partial int SelectedTabIndex { get; set; } = 0;
        public ImportSettingsViewModel ImportSettings { get; } = new();

        // In the future, we will add:
        // public GeneralSettingsViewModel GeneralSettings { get; } = new();
        // public ExportSettingsViewModel ExportSettings { get; } = new();
    }
}