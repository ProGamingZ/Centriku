using Avalonia.Controls;
using Centriku.ViewModels.Settings;
using Avalonia.Controls.Notifications;

namespace Centriku.Views.Settings
{
    public partial class Sf9SettingsView : UserControl
    {
        private WindowNotificationManager? _notificationManager;

        public Sf9SettingsView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            
            // Set up the Notification Manager when the view loads
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                _notificationManager = new WindowNotificationManager(topLevel) { MaxItems = 3 };
            }
        }

        protected override void OnDataContextChanged(System.EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is Sf9SettingsViewModel vm)
            {
                // Connect the ViewModel's text message to our popup UI
                vm.ShowToastMessage = (msg) => 
                {
                    _notificationManager?.Show(new Notification("SF9 Settings", msg, NotificationType.Success));
                };
            }
        }

        // Handles the "Browse..." folder picker button
        public async void OnBrowseFolderClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Default SF9 Export Folder",
                AllowMultiple = false
            });

            if (folders != null && folders.Count > 0)
            {
                if (DataContext is Sf9SettingsViewModel vm)
                {
                    vm.Sf9DefaultExportPath = folders[0].Path.LocalPath;
                }
            }
        }
    }
}