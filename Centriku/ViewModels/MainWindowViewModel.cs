using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty] private bool _isDarkTheme = true;

        [RelayCommand]
        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            
            // This instantly swaps between the "Dark" and "Light" dictionaries in App.axaml
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
            }
        }

        [ObservableProperty] private ViewModelBase? _currentPage;
        [ObservableProperty] private bool _isSidebarOpen = true;
        [ObservableProperty] private double _minWidth = 1024;
        [ObservableProperty] private double _minHeight = 768;
        [ObservableProperty] private WindowState _currentWindowState = WindowState.Normal;

        public MainWindowViewModel()
        {
            Navigate(new DashboardViewModel());
            StartGlobalSecuritySweep();
        }

        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarOpen = !IsSidebarOpen;
        }

        public void Navigate(ViewModelBase viewModel)
        {
            CurrentPage = viewModel;
        }


        private void StartGlobalSecuritySweep()
        {
            var securityTimer = new DispatcherTimer { Interval = System.TimeSpan.FromMinutes(5) };
            securityTimer.Tick += (s, e) =>
            {
                // Placeholder for LicenseManager logic
                bool isLicenseValid = true; // Replace with actual API check later
                
                if (!isLicenseValid)
                {
                    // Forcefully navigate to a locked screen
                    // Navigate(new LicenseExpiredViewModel());
                }
            };
            securityTimer.Start();
        }
    }
}