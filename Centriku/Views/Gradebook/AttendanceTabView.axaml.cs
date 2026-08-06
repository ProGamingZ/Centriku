using Avalonia.Controls;
using Avalonia.Data;
using Centriku.ViewModels;
using System.Linq;
using System.ComponentModel;

namespace Centriku.Views.Gradebook
{
   public partial class AttendanceTabView : UserControl
   {
      public AttendanceTabView()
      {
         InitializeComponent();
      }

      protected override void OnDataContextChanged(System.EventArgs e)
      {
         base.OnDataContextChanged(e);

         if (DataContext is GradebookViewModel vm)
         {
            vm.PropertyChanged -= Vm_PropertyChanged;
            vm.PropertyChanged += Vm_PropertyChanged;

            if (vm.ClassAssessments != null)
            {
               GenerateAttendanceColumns(vm);
            }
         }
      }

      private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
      {
         if ((e.PropertyName == nameof(GradebookViewModel.ClassAssessments) || 
            e.PropertyName == nameof(GradebookViewModel.GridRefreshTrigger)) 
            && sender is GradebookViewModel vm)
         {
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
         foreach (var col in columnsToRemove) grid.Columns.Remove(col);

         // 4. Spawn the Dates
         foreach (var date in vm.AttendanceDates)
         {
            if (vm.SelectedMonthFilter != "All Months" && date.ToString("MMM yyyy") != vm.SelectedMonthFilter)
            {
               continue; 
            }

            var headerPanel = new Avalonia.Controls.StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 5) };
            headerPanel.Children.Add(new Avalonia.Controls.TextBlock { Text = $"{date:dd/MM/yyyy}", FontWeight = Avalonia.Media.FontWeight.Bold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
            headerPanel.Children.Add(new Avalonia.Controls.TextBlock { Text = $"{date:ddd}".ToUpper(), FontSize = 11, Foreground = Avalonia.Media.Brushes.Gray, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
            
            var buttonPanel = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 5, 0, 0) };
            buttonPanel.Children.Add(new Avalonia.Controls.Button { Content = "✏️", Background = Avalonia.Media.Brushes.Transparent, Command = vm.EditRollCallCommand, CommandParameter = date, Padding = new Avalonia.Thickness(5) });
            buttonPanel.Children.Add(new Avalonia.Controls.Button { Content = "🗑️", Background = Avalonia.Media.Brushes.Transparent, Command = vm.DeleteRollCallCommand, CommandParameter = date, Padding = new Avalonia.Thickness(5) });
            headerPanel.Children.Add(buttonPanel);

            var newColumn = new DataGridTemplateColumn
            {
               Header = headerPanel, Width = DataGridLength.Auto, MaxWidth = 150, CanUserSort = false, IsReadOnly = false,
               
               CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((rowData, __) =>
               {
                  var cellGrid = new Avalonia.Controls.Grid { Background = Avalonia.Media.Brushes.Transparent };
                  var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                  tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding($"Cells[{date:yyyy-MM-dd}].Status"));

                  var indicator = new Avalonia.Controls.Shapes.Ellipse { Width = 6, Height = 6, Fill = Avalonia.Media.Brushes.Orange, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Margin = new Avalonia.Thickness(3) };
                  indicator.Bind(Avalonia.Controls.Shapes.Ellipse.IsVisibleProperty, new Avalonia.Data.Binding($"Cells[{date:yyyy-MM-dd}].HasReason"));

                  cellGrid.Children.Add(tb);
                  cellGrid.Children.Add(indicator);

                  cellGrid.PointerPressed += (s, ev) =>
                  {
                     if (ev.GetCurrentPoint(cellGrid).Properties.IsRightButtonPressed)
                     {
                        ev.Handled = true; 
                        if (rowData is AttendanceGridRowViewModel row && vm != null && row.Cells.TryGetValue(date.ToString("yyyy-MM-dd"), out var cellVM))
                        {
                           vm.SelectedAttendanceCell = cellVM;
                           vm.SelectedAttendanceStudentName = $"{row.LastName}, {row.FirstName}";
                           vm.SelectedAttendanceDateDisplay = date.ToString("MMM dd, yyyy");
                           vm.IsAttendancePanelOpen = true; 
                        }
                     }
                  };
                  return cellGrid;
               }),
               
               CellEditingTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((rowData, __) =>
               {
                  var box = new Avalonia.Controls.TextBox { HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center };
                  box.Bind(Avalonia.Controls.TextBox.TextProperty, new Avalonia.Data.Binding($"Cells[{date:yyyy-MM-dd}].Status") { Mode = Avalonia.Data.BindingMode.TwoWay });
                  
                  box.PointerPressed += (s, ev) =>
                  {
                     if (ev.GetCurrentPoint(box).Properties.IsRightButtonPressed)
                     {
                        ev.Handled = true;
                        if (rowData is AttendanceGridRowViewModel row && vm != null && row.Cells.TryGetValue(date.ToString("yyyy-MM-dd"), out var cellVM))
                        {
                           vm.SelectedAttendanceCell = cellVM;
                           vm.SelectedAttendanceStudentName = $"{row.LastName}, {row.FirstName}";
                           vm.SelectedAttendanceDateDisplay = date.ToString("MMM dd, yyyy");
                           vm.IsAttendancePanelOpen = true; 
                        }
                     }
                  };
                  box.AttachedToVisualTree += (sender, args) =>
                  {
                     Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                     {
                        box.SelectAll();
                     }, Avalonia.Threading.DispatcherPriority.Input);
                  };
                  return box;
               })
            };
            grid.Columns.Add(newColumn);
         }
      }

      public void AttendanceGrid_CurrentCellChanged(object sender, System.EventArgs e)
      {
         if (sender is DataGrid grid && grid.CurrentColumn != null && !grid.CurrentColumn.IsReadOnly)
         {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => grid.BeginEdit(), Avalonia.Threading.DispatcherPriority.Input);
         }
      }
   }
}