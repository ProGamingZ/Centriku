using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        [ObservableProperty] private int _totalStudents = 142;
        [ObservableProperty] private int _activeClasses = 4;
        [ObservableProperty] private int _needsAttention = 3;
    }
}