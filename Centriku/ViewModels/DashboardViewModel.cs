using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        [ObservableProperty] public partial int TotalStudents { get; set; } = 142;
        [ObservableProperty] public partial int ActiveClasses { get; set; } = 4;
        [ObservableProperty] public partial int NeedsAttention { get; set; } = 3;
    }
}