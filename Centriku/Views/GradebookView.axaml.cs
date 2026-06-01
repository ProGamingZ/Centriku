using Avalonia.Controls;
using Avalonia.Data;
using Centriku.ViewModels;
using System.Linq;
using System.ComponentModel;
using Avalonia.Controls.Notifications; 

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

            // 1. Find the fixed columns safely
            var attColFirstName = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "FirstName");
            var attColLastName = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "LastName");
            var attColP = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "TotalP");
            var attColL = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "TotalL");
            var attColA = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "TotalA");
            var attColE = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "TotalE");

            // 2. Toggle visibility
            if (attColFirstName != null) attColFirstName.IsVisible = vm.ShowFirstName;
            if (attColLastName != null) attColLastName.IsVisible = vm.ShowLastName;
            if (attColP != null) attColP.IsVisible = vm.ShowTotalP;
            if (attColL != null) attColL.IsVisible = vm.ShowTotalL;
            if (attColA != null) attColA.IsVisible = vm.ShowTotalA;
            if (attColE != null) attColE.IsVisible = vm.ShowTotalE;

            // 3. WIPE CLEAN: Tell the grid to keep ONLY our 6 fixed columns
            var staticColumns = new System.Collections.Generic.List<Avalonia.Controls.DataGridColumn>();
            if (attColFirstName != null) staticColumns.Add(attColFirstName);
            if (attColLastName != null) staticColumns.Add(attColLastName);
            if (attColP != null) staticColumns.Add(attColP);
            if (attColL != null) staticColumns.Add(attColL);
            if (attColA != null) staticColumns.Add(attColA);
            if (attColE != null) staticColumns.Add(attColE);

            var columnsToRemove = grid.Columns.Where(c => !staticColumns.Contains(c)).ToList();

            foreach (var col in columnsToRemove)
            {
                grid.Columns.Remove(col);
            }

            // C. Spawn the actual Dates
            foreach (var date in vm.AttendanceDates)
            {
                if (vm.SelectedMonthFilter != "All Months" && date.ToString("MMM yyyy") != vm.SelectedMonthFilter)
                {
                    continue; // Skip drawing column if doesn't match selected month!
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
                    IsReadOnly = false, // Fast Editing!
                    
                    // === 1. THE DISPLAY VIEW ===
                    CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((rowData, __) =>
                    {
                        var cellGrid = new Avalonia.Controls.Grid { Background = Avalonia.Media.Brushes.Transparent };

                        var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                        tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding($"Cells[{date:yyyy-MM-dd}].Status"));

                        var indicator = new Avalonia.Controls.Shapes.Ellipse
                        {
                            Width = 6, Height = 6, Fill = Avalonia.Media.Brushes.Orange,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                            Margin = new Avalonia.Thickness(3)
                        };
                        indicator.Bind(Avalonia.Controls.Shapes.Ellipse.IsVisibleProperty, new Avalonia.Data.Binding($"Cells[{date:yyyy-MM-dd}].HasReason"));

                        cellGrid.Children.Add(tb);
                        cellGrid.Children.Add(indicator);

                        // === THE FIX: Intercept Right-Click, block Auto-Edit, and slide the panel! ===
                        cellGrid.PointerPressed += (s, ev) =>
                        {
                            if (ev.GetCurrentPoint(cellGrid).Properties.IsRightButtonPressed)
                            {
                                ev.Handled = true; // STOPS the DataGrid from triggering Auto-Edit!
                                
                                if (rowData is AttendanceGridRowViewModel row && vm != null)
                                {
                                    if (row.Cells.TryGetValue(date.ToString("yyyy-MM-dd"), out var cellVM))
                                    {
                                        vm.SelectedAttendanceCell = cellVM;
                                        vm.SelectedAttendanceStudentName = $"{row.LastName}, {row.FirstName}";
                                        vm.SelectedAttendanceDateDisplay = date.ToString("MMM dd, yyyy");
                                        vm.IsAttendancePanelOpen = true; 
                                    }
                                }
                            }
                        };
                        // =============================================================================

                        return cellGrid;
                    }),
                    
                    // === 2. THE EDIT VIEW (TextBox) ===
                    CellEditingTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((rowData, __) =>
                    {
                        var box = new Avalonia.Controls.TextBox { HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center };
                        box.Bind(Avalonia.Controls.TextBox.TextProperty, new Avalonia.Data.Binding($"Cells[{date:yyyy-MM-dd}].Status") { Mode = Avalonia.Data.BindingMode.TwoWay });
                        
                        box.PointerPressed += (s, ev) =>
                        {
                            if (ev.GetCurrentPoint(box).Properties.IsRightButtonPressed)
                            {
                                ev.Handled = true;
                                
                                if (rowData is AttendanceGridRowViewModel row && vm != null)
                                {
                                    if (row.Cells.TryGetValue(date.ToString("yyyy-MM-dd"), out var cellVM))
                                    {
                                        vm.SelectedAttendanceCell = cellVM;
                                        vm.SelectedAttendanceStudentName = $"{row.LastName}, {row.FirstName}";
                                        vm.SelectedAttendanceDateDisplay = date.ToString("MMM dd, yyyy");
                                        vm.IsAttendancePanelOpen = true; 
                                    }
                                }
                            }
                        };
                        // =============================================================================================

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

            // 1. Find the fixed columns safely
            var colLRN = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "StudentID");
            var colFirstName = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "StudentInfo.FirstName");
            var colLastName = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "StudentInfo.LastName");
            var colFinalGrade = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "FinalGradeNumeric");
            var colActions = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Actions");

            // 2. Toggle visibility
            if (colLRN != null) colLRN.IsVisible = vm.ShowLRN;
            if (colFirstName != null) colFirstName.IsVisible = vm.ShowFirstName;
            if (colLastName != null) colLastName.IsVisible = vm.ShowLastName;
            
            if (colFinalGrade != null) 
            {
                colFinalGrade.IsVisible = vm.ShowFinalGrade;
                colFinalGrade.Header = vm.DynamicFinalColumnName;
            }

            // 3. Tell the grid to keep ONLY our 5 fixed columns
            var staticColumns = new System.Collections.Generic.List<Avalonia.Controls.DataGridColumn>();
            if (colLRN != null) staticColumns.Add(colLRN);
            if (colFirstName != null) staticColumns.Add(colFirstName);
            if (colLastName != null) staticColumns.Add(colLastName);
            if (colFinalGrade != null) staticColumns.Add(colFinalGrade);
            if (colActions != null) staticColumns.Add(colActions);

            var columnsToRemove = grid.Columns.Where(c => !staticColumns.Contains(c)).ToList();

            foreach (var col in columnsToRemove)
            {
                grid.Columns.Remove(col);
            }

            // 4. Insert new dynamic columns right before the Final Grade column
            int insertIndex = colFinalGrade != null ? grid.Columns.IndexOf(colFinalGrade) : grid.Columns.Count - 1;
            if (insertIndex < 0) insertIndex = grid.Columns.Count;
            
            // 3A. MODE: SEMESTER AVERAGE 
            if (vm.IsSemesterAverageView)
            {
                if (vm.EducationMode == "Semestral")
                {
                    // Draw Midterm and Final columns
                    var midColumn = new DataGridTemplateColumn { Header = "Midterm", IsVisible = vm.ShowMidtermGrade, CanUserSort = true, SortMemberPath = "MidtermGradeNumeric", IsReadOnly = true, CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) => { var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }; tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding("MidtermGradeDisplay")); return tb; })};
                    grid.Columns.Insert(insertIndex++, midColumn);

                    var finalColumn = new DataGridTemplateColumn { Header = "Final", IsVisible = vm.ShowFinalTermGrade, CanUserSort = true, SortMemberPath = "FinalTermGradeNumeric", IsReadOnly = true, CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) => { var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }; tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding("FinalTermGradeDisplay")); return tb; })};
                    grid.Columns.Insert(insertIndex, finalColumn);
                }
                else // Quarterly
                {
                    // Draw Q1, Q2, Q3, Q4 columns automatically!
                    var quarters = new[] 
                    { 
                        new { Name = "Q1", IsVisible = vm.ShowQ1Grade },
                        new { Name = "Q2", IsVisible = vm.ShowQ2Grade },
                        new { Name = "Q3", IsVisible = vm.ShowQ3Grade },
                        new { Name = "Q4", IsVisible = vm.ShowQ4Grade }
                    };

                    foreach (var q in quarters)
                    {
                        if (!q.IsVisible) continue; // Skip drawing this column if the user unchecked it!

                        var qCol = new DataGridTemplateColumn 
                        { 
                            Header = q.Name, 
                            CanUserSort = true, 
                            SortMemberPath = $"{q.Name}GradeNumeric", 
                            IsReadOnly = true, 
                            CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) => 
                            { 
                                var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }; 
                                tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding($"{q.Name}GradeDisplay")); 
                                return tb; 
                            })
                        };
                        grid.Columns.Insert(insertIndex++, qCol);
                    }
                }
                return; 
            }

            // 3B. MODE: MIDTERM or FINAL
            foreach (var category in vm.CategoryFilters)
            {
                foreach (var filter in category.Assessments)
                {
                    if (!filter.IsVisible) continue; 

                    var assessment = filter.DbModel;

                    //Only draw the column if it belongs to the currently viewed term!
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
                        IsReadOnly = false, 

                        // === 1. THE DISPLAY VIEW (Solid background for reliable clicking!) ===
                        CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                        {
                            var cellGrid = new Avalonia.Controls.Grid { Background = Avalonia.Media.Brushes.Transparent };
                            
                            var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                            tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding($"Scores[{assessment.AssessmentID}].PointsEarnedDisplay"));
                            
                            cellGrid.Children.Add(tb);
                            return cellGrid;
                        }),
                        // === 2. THE EDIT VIEW (Centered Input Box) ===
                        CellEditingTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                        {
                            var box = new Avalonia.Controls.TextBox { HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center };
                            box.Bind(Avalonia.Controls.TextBox.TextProperty, new Avalonia.Data.Binding($"Scores[{assessment.AssessmentID}].PointsEarnedDisplay") { Mode = Avalonia.Data.BindingMode.TwoWay });
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
            if (sender is DataGrid grid && grid.CurrentColumn != null && !grid.CurrentColumn.IsReadOnly)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => grid.BeginEdit(), Avalonia.Threading.DispatcherPriority.Input);
            }
        }
    }
}