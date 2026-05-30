using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        [ObservableProperty] 
        public partial string ClassroomTerminology { get; set; } = "Sections (Asia)";
        
        public ObservableCollection<string> AvailableTerminologies { get; } =
        [
            "Sections (Asia)", 
            "Periods / Blocks (USA)", 
            "Sets / Streams (UK)" 
        ];
    }
}