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
                var newColumn = new DataGridTemplateColumn
                {
                    Header = headerPanel, 
                    Width = DataGridLength.Auto, 
                    MaxWidth = 150, 
                    CanUserSort = false,
                    
                    // The Display View (Centered Text)
                    CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                    {
                        var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                        tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding($"Cells[{date:yyyy-MM-dd}].Status"));
                        return tb;
                    }),
                    
                    // The Edit View (Centered Input Box)
                    CellEditingTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                    {
                        var box = new Avalonia.Controls.TextBox { HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center };
                        box.Bind(Avalonia.Controls.TextBox.TextProperty, new Avalonia.Data.Binding($"Cells[{date:yyyy-MM-dd}].Status") { Mode = Avalonia.Data.BindingMode.TwoWay });
                        return box;
                    })
                };
                grid.Columns.Add(newColumn);
            }
        }

        private void GenerateDynamicColumns(GradebookViewModel vm)
        {
            var grid = this.FindControl<DataGrid>("RosterGrid");
            if (grid == null) return;

            // 1. Toggle basic student info columns
            var lrnCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "LRN");
            if (lrnCol != null) lrnCol.IsVisible = vm.ShowLRN;

            var firstNameCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "First Name");
            if (firstNameCol != null) firstNameCol.IsVisible = vm.ShowFirstName;

            var lastNameCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Last Name");
            if (lastNameCol != null) lastNameCol.IsVisible = vm.ShowLastName;

            var finalGradeCol = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Final Grade" || c.Header?.ToString() == "Semester Average");
            if (finalGradeCol != null) 
            {
                finalGradeCol.IsVisible = vm.ShowFinalGrade;
                // Rename the final column header dynamically based on the view!
                finalGradeCol.Header = vm.SelectedTermView == "Semester Average" ? "Semester Average" : "Final Grade";
            }

            // 2. WIPE CLEAN: Remove all previously generated dynamic columns (Quizzes OR the Midterm/Final summary columns)
            var columnsToRemove = grid.Columns.Where(c => 
                c.Header?.ToString() != "LRN" && 
                c.Header?.ToString() != "Last Name" && 
                c.Header?.ToString() != "First Name" &&
                c.Header?.ToString() != "Final Grade" && 
                c.Header?.ToString() != "Semester Average" && 
                c.Header?.ToString() != "Actions").ToList();

            foreach (var col in columnsToRemove)
            {
                grid.Columns.Remove(col);
            }

            var finalGradeTarget = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Final Grade" || c.Header?.ToString() == "Semester Average");
            int insertIndex = finalGradeTarget != null ? grid.Columns.IndexOf(finalGradeTarget) : grid.Columns.Count - 1;
            
            // === 3A. MODE: SEMESTER AVERAGE ===
            // Only draw the clean, high-level summary columns. Do NOT draw the quizzes.
            if (vm.SelectedTermView == "Semester Average")
            {
                var midColumn = new DataGridTemplateColumn
                {
                    Header = "Midterm", 
                    CanUserSort = true,                       
                    SortMemberPath = "MidtermGradeNumeric",   
                    IsReadOnly = true,
                    CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                    {
                        var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                        tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding("MidtermGradeDisplay"));
                        return tb;
                    })
                };
                grid.Columns.Insert(insertIndex++, midColumn);

                var finalColumn = new DataGridTemplateColumn
                {
                    Header = "Final", 
                    CanUserSort = true,                       
                    SortMemberPath = "FinalTermGradeNumeric",   
                    IsReadOnly = true,
                    CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                    {
                        var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                        tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding("FinalTermGradeDisplay"));
                        return tb;
                    })
                };
                grid.Columns.Insert(insertIndex, finalColumn);
                return; 
            }

            // === 3B. MODE: MIDTERM or FINAL ===
            // Loop through categories and draw the individual quizzes and projects
            foreach (var category in vm.CategoryFilters)
            {
                foreach (var filter in category.Assessments)
                {
                    if (!filter.IsVisible) continue; 

                    var assessment = filter.DbModel;

                    // FILTER: Only draw the column if it belongs to the currently viewed term!
                    if (assessment.GradingPeriod != vm.SelectedTermView) continue;

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

                    var newColumn = new DataGridTemplateColumn
                    {
                        Header = headerPanel,
                        Width = DataGridLength.Auto,
                        MaxWidth = 250,
                        CanUserSort = true,
                        SortMemberPath = $"Scores[{assessment.AssessmentID}].PointsEarned",
                        
                        // The Display View (Centered Text)
                        CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                        {
                            var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                            tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding($"Scores[{assessment.AssessmentID}].PointsEarned"));
                            return tb;
                        }),
                        
                        // The Edit View (Centered Input Box)
                        CellEditingTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                        {
                            var box = new Avalonia.Controls.TextBox { HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center };
                            box.Bind(Avalonia.Controls.TextBox.TextProperty, new Avalonia.Data.Binding($"Scores[{assessment.AssessmentID}].PointsEarned") { Mode = Avalonia.Data.BindingMode.TwoWay });
                            return box;
                        })
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