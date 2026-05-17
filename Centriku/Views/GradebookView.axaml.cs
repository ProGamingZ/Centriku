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
            // If the ClassAssessments list updates (e.g. teacher clicked "Save Assessment"), rebuild the grid!
            if (e.PropertyName == nameof(GradebookViewModel.ClassAssessments) && sender is GradebookViewModel vm)
            {
                GenerateDynamicColumns(vm);
            }
        }

        private void GenerateDynamicColumns(GradebookViewModel vm)
        {
            var grid = this.FindControl<DataGrid>("RosterGrid");
            if (grid == null) return;

            // 1. CLEAR OLD DYNAMIC COLUMNS
            // We safely remove any column that isn't one of our fixed base columns
            var columnsToRemove = grid.Columns.Where(c => 
                c.Header?.ToString() != "LRN" && 
                c.Header?.ToString() != "Last Name" && 
                c.Header?.ToString() != "First Name" && 
                c.Header?.ToString() != "Actions").ToList();

            foreach (var col in columnsToRemove)
            {
                grid.Columns.Remove(col);
            }

            // 2. SPAWN NEW COLUMNS
            // We want to insert the new grading columns right before the "Actions" column
            int insertIndex = grid.Columns.Count - 1; 

            foreach (var assessment in vm.ClassAssessments)
            {
                // --- A. Build the Custom Header ---
                var headerPanel = new Avalonia.Controls.StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 5) };
                
                // 1. The Title
                headerPanel.Children.Add(new Avalonia.Controls.TextBlock { 
                    Text = assessment.Title, 
                    FontWeight = Avalonia.Media.FontWeight.Bold, 
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center 
                });

                // 2. NEW: The Category Badge (e.g., "Written Work")
                headerPanel.Children.Add(new Avalonia.Controls.TextBlock { 
                    Text = $"[{assessment.Category}]", 
                    FontSize = 11, 
                    Foreground = Avalonia.Media.Brushes.DarkCyan, // Gives it a nice distinct color
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center 
                });

                // 3. The Max Score
                headerPanel.Children.Add(new Avalonia.Controls.TextBlock { 
                    Text = $"Max: {assessment.MaxScore}", 
                    FontSize = 11, 
                    Foreground = Avalonia.Media.Brushes.Gray, 
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center 
                });

                // 4. The Edit/Delete Buttons
                var buttonPanel = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 5, 0, 0) };
                
                var editBtn = new Avalonia.Controls.Button { Content = "✏️", Background = Avalonia.Media.Brushes.Transparent, Command = vm.EditAssessmentCommand, CommandParameter = assessment, Padding = new Avalonia.Thickness(5) };
                var delBtn = new Avalonia.Controls.Button { Content = "🗑️", Background = Avalonia.Media.Brushes.Transparent, Command = vm.DeleteAssessmentCommand, CommandParameter = assessment, Padding = new Avalonia.Thickness(5) };

                buttonPanel.Children.Add(editBtn);
                buttonPanel.Children.Add(delBtn);
                headerPanel.Children.Add(buttonPanel);

                // --- B. Create the Column ---
                var newColumn = new DataGridTextColumn
                {
                    Header = headerPanel, // We pass the entire stack panel we just built!
                    MinWidth = 120,
                    Binding = new Binding($"Scores[{assessment.AssessmentID}].PointsEarned")
                };

                grid.Columns.Insert(insertIndex, newColumn);
                insertIndex++;
            }
        }
    }
}