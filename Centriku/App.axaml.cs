using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Centriku.ViewModels;
using Centriku.Views;
using Centriku.Services; 

namespace Centriku
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var dbService = new DatabaseService();
            _ = dbService.InitializeDatabaseAsync(); // '_' to fire-and-forget this background task
            Centriku.Services.StorageService.InitializeDefaultTemplates();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}