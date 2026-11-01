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

         var backupData = grid.ItemsSource;
         grid.ItemsSource = null;

         try
         {
            // 1. Find the fixed columns safely
            var attColFirstName = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "FirstName");
            var attColLastName = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "LastName");
            var attColP = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "TotalP");
            var attColL = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "TotalL");
            var attColA = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "TotalA");
            var attColE = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "TotalE");
            var attColRem = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "RemainingAllowanceText"); 

            // 2. Toggle visibility
            if (attColFirstName != null) attColFirstName.IsVisible = vm.ShowFirstName;
            if (attColLastName != null) attColLastName.IsVisible = vm.ShowLastName;
            if (attColP != null) attColP.IsVisible = vm.ShowTotalP;
            if (attColL != null) attColL.IsVisible = vm.ShowTotalL;
            if (attColA != null) attColA.IsVisible = vm.ShowTotalA;
            if (attColE != null) attColE.IsVisible = vm.ShowTotalE;

            // 3. Dynamically calculate FrozenColumnCount so hidden summary columns never freeze date columns!
            int frozenCount = 1; // "Hours Left" column is always visible
            if (vm.ShowLastName) frozenCount++;
            if (vm.ShowFirstName) frozenCount++;
            if (vm.ShowTotalP) frozenCount++;
            if (vm.ShowTotalL) frozenCount++;
            if (vm.ShowTotalA) frozenCount++;
            if (vm.ShowTotalE) frozenCount++;
            grid.FrozenColumnCount = frozenCount;

            // 4. Keep ONLY our fixed columns
            var staticColumns = new System.Collections.Generic.List<Avalonia.Controls.DataGridColumn>();
            if (attColFirstName != null) staticColumns.Add(attColFirstName);
            if (attColLastName != null) staticColumns.Add(attColLastName);
            if (attColP != null) staticColumns.Add(attColP);
            if (attColL != null) staticColumns.Add(attColL);
            if (attColA != null) staticColumns.Add(attColA);
            if (attColE != null) staticColumns.Add(attColE);
            if (attColRem != null) staticColumns.Add(attColRem); 

            var columnsToRemove = grid.Columns.Where(c => !staticColumns.Contains(c)).ToList();
            foreach (var col in columnsToRemove) grid.Columns.Remove(col);

            // 5. Spawn the Dates with Centralized Theme Brushes
            foreach (var date in vm.AttendanceDates)
            {
               if (vm.SelectedMonthFilter != "All Months" && date.ToString("MMM yyyy") != vm.SelectedMonthFilter)
               {
                  continue; 
               }

               var headerPanel = new Avalonia.Controls.StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 4) };
               
               var dateTb = new Avalonia.Controls.TextBlock { Text = $"{date:dd/MM/yyyy}", FontWeight = Avalonia.Media.FontWeight.Bold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
               dateTb[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMainBrush");
               headerPanel.Children.Add(dateTb);

               var dayTb = new Avalonia.Controls.TextBlock { Text = $"{date:ddd}".ToUpper(), FontSize = 11, FontWeight = Avalonia.Media.FontWeight.SemiBold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
               dayTb[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMutedBrush");
               headerPanel.Children.Add(dayTb);
               
               var buttonPanel = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 4, 0, 0) };

               var editIcon = new Avalonia.Controls.TextBlock { FontSize = 15 };
               editIcon.Classes.Add("Icon");
               editIcon[!Avalonia.Controls.TextBlock.TextProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("IconEdit");
               editIcon[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMainBrush");

               var delIcon = new Avalonia.Controls.TextBlock { FontSize = 15 };
               delIcon.Classes.Add("Icon");
               delIcon[!Avalonia.Controls.TextBlock.TextProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("IconDelete");
               delIcon[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("DangerBrush");

               buttonPanel.Children.Add(new Avalonia.Controls.Button { Content = editIcon, Background = Avalonia.Media.Brushes.Transparent, Command = vm.EditRollCallCommand, CommandParameter = date, Padding = new Avalonia.Thickness(4) });
               buttonPanel.Children.Add(new Avalonia.Controls.Button { Content = delIcon, Background = Avalonia.Media.Brushes.Transparent, Command = vm.DeleteRollCallCommand, CommandParameter = date, Padding = new Avalonia.Thickness(4) });
               headerPanel.Children.Add(buttonPanel);

               var newColumn = new DataGridTemplateColumn
               {
                  Header = headerPanel, Width = DataGridLength.Auto, MaxWidth = 150, CanUserSort = false, IsReadOnly = false,
                  
                  CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((rowData, __) =>
                  {
                     var cellGrid = new Avalonia.Controls.Grid { Background = Avalonia.Media.Brushes.Transparent };
                     var tb = new Avalonia.Controls.TextBlock { FontWeight = Avalonia.Media.FontWeight.SemiBold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                     tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding($"Cells[{date:yyyy-MM-dd}].Status"));

                     var indicator = new Avalonia.Controls.Shapes.Ellipse { Width = 6, Height = 6, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Margin = new Avalonia.Thickness(3) };
                     indicator[!Avalonia.Controls.Shapes.Shape.FillProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("WarningBrush");
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
         finally
         {
            grid.ItemsSource = backupData;
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