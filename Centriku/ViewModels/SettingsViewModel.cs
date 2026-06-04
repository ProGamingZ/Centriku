using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.ViewModels.Settings;

namespace Centriku.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        // This holds the isolated logic for the Import Tab!
        public ImportSettingsViewModel ImportSettings { get; } = new();

        // In the future, we will add:
        // public GeneralSettingsViewModel GeneralSettings { get; } = new();
        // public ExportSettingsViewModel ExportSettings { get; } = new();
    }
}