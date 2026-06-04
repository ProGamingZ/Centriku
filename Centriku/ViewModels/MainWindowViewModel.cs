using Avalonia.Threading;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Styling;
using Centriku.Services; 

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
        
        // --- 1. CACHE ALL VIEWMODELS HERE ---
        private readonly DashboardViewModel _dashboardViewModel;
        private readonly MyClassesViewModel _myClassesViewModel;
        private readonly PoliciesViewModel _policiesViewModel;
        private readonly DirectoryViewModel _directoryViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        // ------------------------------------

        public IRelayCommand ToggleSidebarCommand { get; }
        public IRelayCommand ToggleThemeCommand { get; }
        public IRelayCommand NavigateToDashboardCommand { get; }
        public IRelayCommand NavigateToMyClassesCommand { get; }
        public IRelayCommand NavigateToPoliciesCommand { get; }
        public IRelayCommand NavigateToDirectoryCommand { get; }
        public IRelayCommand NavigateToSettingsCommand { get; }

        public MainWindowViewModel()
        {
            // 2. INITIALIZE THEM EXACTLY ONCE
            _dashboardViewModel = new DashboardViewModel(Navigate);
            _myClassesViewModel = new MyClassesViewModel(Navigate);
            _policiesViewModel = new PoliciesViewModel();
            _directoryViewModel = new DirectoryViewModel();
            _settingsViewModel = new SettingsViewModel();

            ToggleSidebarCommand = new RelayCommand(() => IsSidebarOpen = !IsSidebarOpen);
            ToggleThemeCommand = new RelayCommand(() => 
            {
                IsDarkTheme = !IsDarkTheme;
                Application.Current?.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
            });
            
            // 3. UPDATE ALL BUTTONS TO USE THE CACHED VARIABLES
            NavigateToDashboardCommand = new RelayCommand(() => Navigate(_dashboardViewModel));
            NavigateToMyClassesCommand = new RelayCommand(() => Navigate(_myClassesViewModel));
            NavigateToPoliciesCommand = new RelayCommand(() => Navigate(_policiesViewModel));
            NavigateToDirectoryCommand = new RelayCommand(() => Navigate(_directoryViewModel));
            NavigateToSettingsCommand = new RelayCommand(() => Navigate(_settingsViewModel));

            // Global Navigation Glue
            Centriku.ViewModels.DirectoryViewModel.OnNavigateToSettingsBulkImportTab += () => 
            {
                _settingsViewModel.SelectedTabIndex = 1;
                Navigate(_settingsViewModel);
            };

            Centriku.ViewModels.Settings.ImportSettingsViewModel.OnNavigateToDirectoryBulkImportTab += () => 
            {
                _directoryViewModel.SelectedTabIndex = 1;
                Navigate(_directoryViewModel);
            };

            BootUpApplication();
        }

        private async void BootUpApplication()
        {
            var dbService = new DatabaseService();
            await dbService.InitializeDatabaseAsync();
            Navigate(_dashboardViewModel);
            
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
                bool isLicenseValid = true; 
                if (!isLicenseValid) { }
            };
            securityTimer.Start();
        }
    }
}