using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Centriku.ViewModels;

namespace Centriku.Views.Directory
{
    public partial class BulkImportTab : UserControl
    {
        public BulkImportTab()
        {
            InitializeComponent();
        }

        // NEW: Triggers the preview table to load its data from SQLite when this tab is opened
        protected override async void OnDataContextChanged(System.EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is DirectoryViewModel vm)
            {
                await vm.LoadImportSettingsPreviewAsync();
            }
        }

        private async void OnImportCsvClicked(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Student Roster",
                AllowMultiple = false,
                FileTypeFilter = new[] 
                { 
                    new FilePickerFileType("Supported Data Files") { Patterns = new[] { "*.csv", "*.xlsx" } },
                    new FilePickerFileType("CSV Document (*.csv)") { Patterns = new[] { "*.csv" } },
                    new FilePickerFileType("Excel Workbook (*.xlsx)") { Patterns = new[] { "*.xlsx" } }
                }
            });

            if (files.Count >= 1)
            {
                var filePath = files[0].Path.LocalPath;
                if (DataContext is DirectoryViewModel vm)
                {
                    await vm.ProcessBulkImportAsync(filePath);
                }
            }
        }
    }
}