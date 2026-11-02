using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services; 

namespace Centriku.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;
        private bool _isLoadingAppearance = false;

        [ObservableProperty] public partial ViewModelBase? CurrentPage { get; set; }
        [ObservableProperty] public partial bool IsSidebarOpen { get; set; } = true;
        [ObservableProperty] public partial double MinWidth { get; set; } = 1024;
        [ObservableProperty] public partial double MinHeight { get; set; } = 700;
        [ObservableProperty] public partial WindowState CurrentWindowState { get; set; } = WindowState.Maximized;

        // --- APPEARANCE & EYE-COMFORT STATE ---
        public ObservableCollection<string> AvailableThemes { get; } = new(ThemeService.GetThemeNames());

        [ObservableProperty] public partial string SelectedTheme { get; set; } = "NwSSU Forest";
        partial void OnSelectedThemeChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            ThemeService.ApplyTheme(value, IsDarkTheme);
            if (!_isLoadingAppearance) _ = SaveAppearanceSettingsAsync();
        }

        [ObservableProperty] public partial bool IsDarkTheme { get; set; } = false;
        partial void OnIsDarkThemeChanged(bool value)
        {
            ThemeService.ApplyTheme(SelectedTheme, value);
            if (!_isLoadingAppearance) _ = SaveAppearanceSettingsAsync();
        }

        [ObservableProperty] public partial double ScreenBrightness { get; set; } = 100.0;
        partial void OnScreenBrightnessChanged(double value)
        {
            double clamped = Math.Clamp(value, 50.0, 100.0);
            // Maps 100% brightness -> 0.0 dimmer opacity; 50% brightness -> 0.45 dimmer opacity
            ScreenDimmerOpacity = Math.Round((100.0 - clamped) / 100.0 * 0.90, 3);
            BrightnessDisplayText = $"{Math.Round(clamped)}%";
            if (!_isLoadingAppearance) _ = SaveAppearanceSettingsAsync();
        }

        [ObservableProperty] public partial double ScreenDimmerOpacity { get; set; } = 0.0;
        [ObservableProperty] public partial string BrightnessDisplayText { get; set; } = "100%";

        // --- ACTIVE TAB TRACKERS ---
        [ObservableProperty] public partial bool IsDashboardActive { get; set; } = true;
        [ObservableProperty] public partial bool IsMyClassesActive { get; set; } = false;
        [ObservableProperty] public partial bool IsPoliciesActive { get; set; } = false;
        [ObservableProperty] public partial bool IsDirectoryActive { get; set; } = false;
        [ObservableProperty] public partial bool IsSettingsActive { get; set; } = false;

        // --- CACHED VIEWMODELS ---
        private readonly DashboardViewModel _dashboardViewModel;
        private readonly MyClassesViewModel _myClassesViewModel;
        private readonly PoliciesViewModel _policiesViewModel;
        private readonly DirectoryViewModel _directoryViewModel;
        private readonly SettingsViewModel _settingsViewModel;

        public IRelayCommand ToggleSidebarCommand { get; }
        public IRelayCommand ToggleThemeCommand { get; }
        public IRelayCommand ResetBrightnessCommand { get; }
        public IRelayCommand NavigateToDashboardCommand { get; }
        public IRelayCommand NavigateToMyClassesCommand { get; }
        public IRelayCommand NavigateToPoliciesCommand { get; }
        public IRelayCommand NavigateToDirectoryCommand { get; }
        public IRelayCommand NavigateToSettingsCommand { get; }

        public MainWindowViewModel()
        {
            _dbService = new DatabaseService();
            _dashboardViewModel = new DashboardViewModel(_dbService, Navigate);
            _myClassesViewModel = new MyClassesViewModel(_dbService, Navigate);
            _policiesViewModel = new PoliciesViewModel(_dbService);
            _directoryViewModel = new DirectoryViewModel(_dbService);
            _settingsViewModel = new SettingsViewModel(_dbService);

            ToggleSidebarCommand = new RelayCommand(() => IsSidebarOpen = !IsSidebarOpen);
            ToggleThemeCommand = new RelayCommand(() => IsDarkTheme = !IsDarkTheme);
            ResetBrightnessCommand = new RelayCommand(() => ScreenBrightness = 100.0);
            
            NavigateToDashboardCommand = new RelayCommand(OnNavigateToDashboard);
            NavigateToMyClassesCommand = new RelayCommand(OnNavigateToMyClasses);
            NavigateToPoliciesCommand = new RelayCommand(OnNavigateToPolicies);
            NavigateToDirectoryCommand = new RelayCommand(OnNavigateToDirectory);
            NavigateToSettingsCommand = new RelayCommand(() => { SetActiveTab("Settings"); Navigate(_settingsViewModel); });

            DirectoryViewModel.OnNavigateToSettingsBulkImportTab += () => 
            {
                SetActiveTab("Settings");
                _settingsViewModel.SelectedTabIndex = 1;
                Navigate(_settingsViewModel);
            };

            Centriku.ViewModels.Settings.ImportSettingsViewModel.OnNavigateToDirectoryBulkImportTab += () => 
            {
                SetActiveTab("Directory");
                _directoryViewModel.SelectedTabIndex = 1;
                Navigate(_directoryViewModel);
            };

            BootUpApplication();
        }

        private void SetActiveTab(string tabName)
        {
            IsDashboardActive = tabName == "Dashboard";
            IsMyClassesActive = tabName == "MyClasses";
            IsPoliciesActive = tabName == "Policies";
            IsDirectoryActive = tabName == "Directory";
            IsSettingsActive = tabName == "Settings";
        }

        private async void BootUpApplication()
        {
            await _dbService.InitializeDatabaseAsync();
            await LoadAppearanceSettingsAsync();
            SetActiveTab("Dashboard");
            Navigate(_dashboardViewModel);
            
            StartGlobalSecuritySweep();
        }

        private async Task LoadAppearanceSettingsAsync()
        {
            try
            {
                _isLoadingAppearance = true;
                var db = _dbService.GetConnection();
                var settings = await db.Table<AppSettings>().FirstOrDefaultAsync();
                if (settings != null)
                {
                    SelectedTheme = AvailableThemes.Contains(settings.SelectedThemeName)
                        ? settings.SelectedThemeName
                        : "NwSSU Forest";
                    IsDarkTheme = settings.IsDarkMode;
                    ScreenBrightness = settings.ScreenBrightness >= 50 && settings.ScreenBrightness <= 100
                        ? settings.ScreenBrightness
                        : 100.0;
                }
                ThemeService.ApplyTheme(SelectedTheme, IsDarkTheme);
            }
            catch { }
            finally
            {
                _isLoadingAppearance = false;
            }
        }

        private async Task SaveAppearanceSettingsAsync()
        {
            try
            {
                await DatabaseService.WaitForDatabaseReadyAsync();
                var db = _dbService.GetConnection();
                var settings = await db.Table<AppSettings>().FirstOrDefaultAsync();
                if (settings != null)
                {
                    settings.SelectedThemeName = SelectedTheme;
                    settings.IsDarkMode = IsDarkTheme;
                    settings.ScreenBrightness = ScreenBrightness;
                    await db.UpdateAsync(settings);
                }
            }
            catch { }
        }

        public void Navigate(ViewModelBase viewModel)
        {
            CurrentPage = viewModel;
        }

        private static void StartGlobalSecuritySweep()
        {
            var securityTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            securityTimer.Tick += (s, e) =>
            {
                bool isLicenseValid = true; 
                if (!isLicenseValid) { }
            };
            securityTimer.Start();
        }
    
        private async void OnNavigateToDashboard()
        {
            SetActiveTab("Dashboard");
            await _dashboardViewModel.LoadDashboardDataAsync(); 
            Navigate(_dashboardViewModel);
        }

        private async void OnNavigateToMyClasses()
        {
            SetActiveTab("MyClasses");
            await _myClassesViewModel.RefreshDataAsync(); 
            Navigate(_myClassesViewModel);
        }

        private async void OnNavigateToPolicies()
        {
            SetActiveTab("Policies");
            await _policiesViewModel.LoadSavedTemplatesAsync(); 
            Navigate(_policiesViewModel);
        }
        
        private async void OnNavigateToDirectory()
        {
            SetActiveTab("Directory");
            await _directoryViewModel.LoadStudentsAsync();
            Navigate(_directoryViewModel);
        }
    }
}