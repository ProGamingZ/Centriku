using Avalonia.Controls;
using Avalonia.Data;
using Centriku.ViewModels;
using System.Linq;
using System.ComponentModel;

namespace Centriku.Views
{
    public partial class GradebookView : UserControl
    {
        public GradebookView()
        {
            InitializeComponent();
        }

        // This triggers when the ViewModel is attached to the View
        protected override void OnDataContextChanged(System.EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is GradebookViewModel vm)
            {
                // Listen for changes so we know exactly when the database finishes loading
                vm.PropertyChanged -= Vm_PropertyChanged;
                vm.PropertyChanged += Vm_PropertyChanged;

                // If data is already loaded, spawn the columns immediately
                if (vm.ClassAssessments != null)
                {
                    GenerateDynamicColumns(vm);
                }
            }
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Rebuild the grid if the assessments change OR if the teacher clicks a view filter checkbox!
            if ((e.PropertyName == nameof(GradebookViewModel.ClassAssessments) || 
                 e.PropertyName == nameof(GradebookViewModel.GridRefreshTrigger)) 
                 && sender is GradebookViewModel vm)
            {
                GenerateDynamicColumns(vm);
            }
        }

        private void GenerateDynamicColumns(GradebookViewModel vm)
        {
            var grid = this.FindControl<DataGrid>("RosterGrid");
            if (grid == null) return;

            // --- A. TOGGLE FIXED STUDENT COLUMNS ---
            var lrnCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "LRN");
            if (lrnCol != null) lrnCol.IsVisible = vm.ShowLRN;

            var lastNameCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Last Name");
            if (lastNameCol != null) lastNameCol.IsVisible = vm.ShowLastName;

            var firstNameCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "First Name");
            if (firstNameCol != null) firstNameCol.IsVisible = vm.ShowFirstName;

            // --- B. CLEAR OLD DYNAMIC COLUMNS ---
            var columnsToRemove = grid.Columns.Where(c => 
                c.Header?.ToString() != "LRN" && 
                c.Header?.ToString() != "Last Name" && 
                c.Header?.ToString() != "First Name" && 
                c.Header?.ToString() != "Actions").ToList();

            foreach (var col in columnsToRemove)
            {
                grid.Columns.Remove(col);
            }

            // --- C. SPAWN ONLY VISIBLE COLUMNS ---
            int insertIndex = grid.Columns.Count - 1; 

            // We loop through our new Category Filters instead of the raw database models
            foreach (var category in vm.CategoryFilters)
            {
                foreach (var filter in category.Assessments)
                {
                    // THE MAGIC: If the teacher unchecked this box, skip drawing the column!
                    if (!filter.IsVisible) continue; 

                    var assessment = filter.DbModel;

                    // Build the Custom Header
                    var headerPanel = new Avalonia.Controls.StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 5) };
                    headerPanel.Children.Add(new Avalonia.Controls.TextBlock { Text = assessment.Title, FontWeight = Avalonia.Media.FontWeight.Bold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
                    headerPanel.Children.Add(new Avalonia.Controls.TextBlock { Text = $"[{assessment.Category}]", FontSize = 11, Foreground = Avalonia.Media.Brushes.DarkCyan, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
                    headerPanel.Children.Add(new Avalonia.Controls.TextBlock { Text = $"Max: {assessment.MaxScore}", FontSize = 11, Foreground = Avalonia.Media.Brushes.Gray, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });

                    var buttonPanel = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 5, 0, 0) };
                    var editBtn = new Avalonia.Controls.Button { Content = "✏️", Background = Avalonia.Media.Brushes.Transparent, Command = vm.EditAssessmentCommand, CommandParameter = assessment, Padding = new Avalonia.Thickness(5) };
                    var delBtn = new Avalonia.Controls.Button { Content = "🗑️", Background = Avalonia.Media.Brushes.Transparent, Command = vm.DeleteAssessmentCommand, CommandParameter = assessment, Padding = new Avalonia.Thickness(5) };

                    buttonPanel.Children.Add(editBtn);
                    buttonPanel.Children.Add(delBtn);
                    headerPanel.Children.Add(buttonPanel);

                    var newColumn = new DataGridTextColumn
                    {
                        Header = headerPanel,
                        MinWidth = 120,
                        Binding = new Binding($"Scores[{assessment.AssessmentID}].PointsEarned")
                    };

                    grid.Columns.Insert(insertIndex, newColumn);
                    insertIndex++;
                }
            }
        }    
    
        private void RosterGrid_CurrentCellChanged(object? sender, System.EventArgs e)
        {
            if (sender is DataGrid grid && grid.CurrentColumn != null)
            {
                // Only trigger auto-edit if it is one of our dynamic grading columns (which are NOT read-only)
                // We also check that it's a TextColumn to avoid accidentally triggering the Actions button column
                if (!grid.CurrentColumn.IsReadOnly && grid.CurrentColumn is DataGridTextColumn)
                {
                    // We use the UI Thread Dispatcher to wait 1 millisecond for the grid to finish moving 
                    // the highlight before we force the cursor inside the box. This prevents visual glitching.
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        grid.BeginEdit();
                    }, Avalonia.Threading.DispatcherPriority.Input);
                }
            }
        }
    }
}