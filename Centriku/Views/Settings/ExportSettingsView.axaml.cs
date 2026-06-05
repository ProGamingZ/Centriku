using Avalonia.Controls;
using Centriku.ViewModels.Settings;
using Avalonia.Controls.Notifications;

namespace Centriku.Views.Settings
{
   public partial class ExportSettingsView : UserControl
   {
      private WindowNotificationManager? _notificationManager;

      public ExportSettingsView()
      {
         InitializeComponent();
      }

      protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
      {
         base.OnAttachedToVisualTree(e);
         
         var topLevel = TopLevel.GetTopLevel(this);
         if (topLevel != null)
         {
            _notificationManager = new WindowNotificationManager(topLevel) { MaxItems = 3 };
         }
      }

      protected override void OnDataContextChanged(System.EventArgs e)
      {
         base.OnDataContextChanged(e);

         if (DataContext is ExportSettingsViewModel vm)
         {
            vm.ShowToastMessage = (msg) => 
            {
               _notificationManager?.Show(new Notification("Export Settings", msg, NotificationType.Success));
            };
         }
      }

      public async void OnBrowseFolderClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
      {
         var topLevel = TopLevel.GetTopLevel(this);
         if (topLevel == null) return;

         var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
         {
            Title = "Select Default Export Folder",
            AllowMultiple = false
         });

         if (folders != null && folders.Count > 0)
         {
            if (DataContext is ExportSettingsViewModel vm)
            {
               vm.DefaultExportFolderPath = folders[0].Path.LocalPath;
            }
         }
      }
   }
}