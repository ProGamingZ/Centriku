using Avalonia.Controls;
using Avalonia.Data;
using Centriku.ViewModels;
using System.Linq;
using System.ComponentModel;
using Avalonia.Controls.Notifications; // NEEDED FOR TOASTS!

namespace Centriku.Views
{
    public partial class GradebookView : UserControl
    {
        // 1. Declare the Notification Manager
        private WindowNotificationManager? _notificationManager;

        public GradebookView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                _notificationManager = new WindowNotificationManager(topLevel) { MaxItems = 3 };
            }
        }

        protected override void OnDataContextChanged(System.EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is GradebookViewModel vm)
            {
                // 3. Connect the ViewModel's Toast action to our visual Toast Manager!
                vm.ShowToastMessage = (msg) => 
                {
                    _notificationManager?.Show(new Notification("Notification", msg, NotificationType.Warning));
                };

                vm.PropertyChanged -= Vm_PropertyChanged;
                vm.PropertyChanged += Vm_PropertyChanged;

                if (vm.ClassAssessments != null)
                {
                    GenerateDynamicColumns(vm);
                    GenerateAttendanceColumns(vm); // MUST CALL THIS HERE
                }
            }
        }
        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == nameof(GradebookViewModel.ClassAssessments) || 
                 e.PropertyName == nameof(GradebookViewModel.GridRefreshTrigger)) 
                 && sender is GradebookViewModel vm)
            {
                GenerateDynamicColumns(vm);
                GenerateAttendanceColumns(vm); 
            }
        }

        private void GenerateAttendanceColumns(GradebookViewModel vm)
        {
            var grid = this.FindControl<DataGrid>("AttendanceGrid");
            if (grid == null) return;

            var lastNameCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Last Name");
            if (lastNameCol != null) lastNameCol.IsVisible = vm.ShowLastName;

            var firstNameCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "First Name");
            if (firstNameCol != null) firstNameCol.IsVisible = vm.ShowFirstName;

            // A. Toggle the Summary Columns
            var tpCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "P");
            if (tpCol != null) tpCol.IsVisible = vm.ShowTotalP;

            var tlCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "L");
            if (tlCol != null) tlCol.IsVisible = vm.ShowTotalL;

            var taCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "A");
            if (taCol != null) taCol.IsVisible = vm.ShowTotalA;

            // B. Clear Old Dynamic Date Columns
            var fixedHeaders = new[] { "Last Name", "First Name", "P", "L", "A" };
            var columnsToRemove = grid.Columns.Where(c => !fixedHeaders.Contains(c.Header?.ToString())).ToList();
            foreach (var col in columnsToRemove) grid.Columns.Remove(col);

            // C. Spawn the actual Dates
            foreach (var date in vm.AttendanceDates)
            {
                if (vm.SelectedMonthFilter != "All Months" && date.ToString("MMM yyyy") != vm.SelectedMonthFilter)
                {
                    continue; // Skip drawing this column if it doesn't match the selected month!
                }

                // 1. Create a StackPanel for the header exactly like the Grades tab
                var headerPanel = new Avalonia.Controls.StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 5) };
                
                var dateText = new Avalonia.Controls.TextBlock { Text = $"{date:dd/MM/yyyy}", FontWeight = Avalonia.Media.FontWeight.Bold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                var dayText = new Avalonia.Controls.TextBlock { Text = $"{date:ddd}".ToUpper(), FontSize = 11, Foreground = Avalonia.Media.Brushes.Gray, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                
                headerPanel.Children.Add(dateText);
                headerPanel.Children.Add(dayText);

                // 2. Create the Edit and Delete Buttons
                var buttonPanel = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 5, 0, 0) };
                var editBtn = new Avalonia.Controls.Button { Content = "✏️", Background = Avalonia.Media.Brushes.Transparent, Command = vm.EditRollCallCommand, CommandParameter = date, Padding = new Avalonia.Thickness(5) };
                var delBtn = new Avalonia.Controls.Button { Content = "🗑️", Background = Avalonia.Media.Brushes.Transparent, Command = vm.DeleteRollCallCommand, CommandParameter = date, Padding = new Avalonia.Thickness(5) };

                buttonPanel.Children.Add(editBtn);
                buttonPanel.Children.Add(delBtn);
                headerPanel.Children.Add(buttonPanel);

                // 3. Attach it to the Column
                var newColumn = new DataGridTextColumn
                {
                    Header = headerPanel, 
                    Binding = new Binding($"Cells[{date:yyyy-MM-dd}].Status"),
                    Width = DataGridLength.Auto, 
                    MaxWidth = 150, 
                    CanUserSort = false 
                };
                grid.Columns.Add(newColumn);
            }
        }

        private void GenerateDynamicColumns(GradebookViewModel vm)
        {
            var grid = this.FindControl<DataGrid>("RosterGrid");
            if (grid == null) return;

            var lrnCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "LRN");
            if (lrnCol != null) lrnCol.IsVisible = vm.ShowLRN;

            var firstNameCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "First Name");
            if (firstNameCol != null) firstNameCol.IsVisible = vm.ShowFirstName;

            var lastNameCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Last Name");
            if (lastNameCol != null) lastNameCol.IsVisible = vm.ShowLastName;

            var finalGradeCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Final Grade");
            if (finalGradeCol != null) finalGradeCol.IsVisible = vm.ShowFinalGrade;

            var columnsToRemove = grid.Columns.Where(c => 
                c.Header?.ToString() != "LRN" && 
                c.Header?.ToString() != "Last Name" && 
                c.Header?.ToString() != "First Name" &&
                c.Header?.ToString() != "Final Grade" && 
                c.Header?.ToString() != "Actions").ToList();

            foreach (var col in columnsToRemove)
            {
                grid.Columns.Remove(col);
            }

            int insertIndex = grid.Columns.Count - 1; 

            foreach (var category in vm.CategoryFilters)
            {
                foreach (var filter in category.Assessments)
                {
                    if (!filter.IsVisible) continue; 

                    var assessment = filter.DbModel;

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
                        Width = DataGridLength.Auto,
                        MaxWidth = 250,
                        Binding = new Binding($"Scores[{assessment.AssessmentID}].PointsEarned"),
                        CanUserSort = true,
                        SortMemberPath = $"Scores[{assessment.AssessmentID}].PointsEarned"
                    };

                    grid.Columns.Insert(insertIndex, newColumn);
                    insertIndex++;
                }
            }
        }    
    
        public void RosterGrid_CurrentCellChanged(object sender, System.EventArgs e)
        {
            TriggerAutoEdit(sender);
        }

        public void AttendanceGrid_CurrentCellChanged(object sender, System.EventArgs e)
        {
            TriggerAutoEdit(sender);
        }

        private static void TriggerAutoEdit(object? sender)
        {
            if (sender is DataGrid grid && grid.CurrentColumn != null)
            {
                if (!grid.CurrentColumn.IsReadOnly && grid.CurrentColumn is DataGridTextColumn)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => grid.BeginEdit(), Avalonia.Threading.DispatcherPriority.Input);
                }
            }
        }
    }
}