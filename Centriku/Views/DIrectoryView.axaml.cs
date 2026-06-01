using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Centriku.ViewModels;

namespace Centriku.Views
{
    public partial class DirectoryView : UserControl
    {
        public DirectoryView()
        {
            InitializeComponent();
        }

        private async void OnImportCsvClicked(object? sender, RoutedEventArgs e)
        {
            // 1. Get the current operating system window
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            // 2. Open the File Picker, restricting it to only .csv files
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Student Roster (CSV)",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Supported Data Files") 
                    { 
                        Patterns = ["*.csv", "*.xlsx"] 
                    },
                    new FilePickerFileType("CSV Document (*.csv)") 
                    { 
                        Patterns = ["*.csv"] 
                    },
                    new FilePickerFileType("Excel Workbook (*.xlsx)") 
                    { 
                        Patterns = ["*.xlsx"] 
                    }
                ]
            });

            // 3. If the user selected a file, pass the path to the ViewModel to process
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