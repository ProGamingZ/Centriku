using Avalonia.Threading;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Styling;

namespace Centriku.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty] public partial ViewModelBase? CurrentPage { get; set; }
        [ObservableProperty] public partial bool IsSidebarOpen { get; set; } = true;
        [ObservableProperty] public partial double MinWidth { get; set; } = 1024;
        [ObservableProperty] public partial double MinHeight { get; set; } = 768;
        [ObservableProperty] public partial WindowState CurrentWindowState { get; set; } = WindowState.Normal;
        [ObservableProperty] public partial bool IsDarkTheme { get; set; } = true;
        public IRelayCommand ToggleSidebarCommand { get; }
        public IRelayCommand ToggleThemeCommand { get; }
        [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;
        partial void OnSearchQueryChanged(string value)
        {
            // We will eventually put our SQLite global search logic here.
            // e.g., var results = DatabaseService.SearchStudents(value);
        }

        public MainWindowViewModel()
        {
            ToggleSidebarCommand = new RelayCommand(() => IsSidebarOpen = !IsSidebarOpen);
            ToggleThemeCommand = new RelayCommand(() => 
            {
                IsDarkTheme = !IsDarkTheme;
                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
                }
            });
            
            Navigate(new DashboardViewModel());
            StartGlobalSecuritySweep();
        }

        public void Navigate(ViewModelBase viewModel)
        {
            CurrentPage = viewModel;
        }

        private static void StartGlobalSecuritySweep()
        {
            var securityTimer = new DispatcherTimer { Interval = System.TimeSpan.FromMinutes(5) };
            securityTimer.Tick += (s, e) =>
            {
                // Placeholder for LicenseManager logic
                bool isLicenseValid = true; 
                
                if (!isLicenseValid)
                {
                    // Navigate(new LicenseExpiredViewModel());
                }
            };
            securityTimer.Start();
        }
    }
}