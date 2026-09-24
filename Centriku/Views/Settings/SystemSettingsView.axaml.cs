using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Centriku.ViewModels.Settings;
using System;

namespace Centriku.Views.Settings
{
    public partial class SystemSettingsView : UserControl
    {
        public SystemSettingsView()
        {
            InitializeComponent();
        }

        private async void OnBackupClicked(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            // Generate a smart default file name
            string defaultName = $"CentrikuBackup_{DateTime.Now:yyyyMMdd}.db";

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Database Backup",
                SuggestedFileName = defaultName,
                DefaultExtension = "db",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("SQLite Database") { Patterns = new[] { "*.db", "*.sqlite" } }
                }
            });

            if (file != null && DataContext is SystemSettingsViewModel vm)
            {
                await vm.ProcessBackupAsync(file.Path.LocalPath);
            }
        }

        private async void OnRestoreClicked(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Database Backup to Restore",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("SQLite Database") { Patterns = new[] { "*.db", "*.sqlite" } }
                }
            });

            if (files.Count >= 1 && DataContext is SystemSettingsViewModel vm)
            {
                await vm.ProcessRestoreAsync(files[0].Path.LocalPath);
            }
        }
    }
}