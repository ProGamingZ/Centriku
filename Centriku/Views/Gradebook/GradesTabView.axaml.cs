using Avalonia.Controls;
using Avalonia.Data;
using Centriku.ViewModels;
using System.Linq;
using System.ComponentModel;

namespace Centriku.Views.Gradebook
{
   public partial class GradesTabView : UserControl
   {
      public GradesTabView()
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
               GenerateDynamicColumns(vm);
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
                  if (!q.IsVisible) continue; 

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

                  CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                  {
                     var cellGrid = new Avalonia.Controls.Grid { Background = Avalonia.Media.Brushes.Transparent };
                     var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                     tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding($"Scores[{assessment.AssessmentID}].PointsEarnedDisplay"));
                     cellGrid.Children.Add(tb);
                     return cellGrid;
                  }),
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
         if (sender is DataGrid grid && grid.CurrentColumn != null && !grid.CurrentColumn.IsReadOnly)
         {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => grid.BeginEdit(), Avalonia.Threading.DispatcherPriority.Input);
         }
      }
   
      public async void OnChangeExportFolderClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
      {
         var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
         if (topLevel == null) return;

         var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
         {
            Title = "Select Export Destination",
            AllowMultiple = false
         });

         if (folders != null && folders.Count > 0)
         {
            if (DataContext is GradebookViewModel vm)
            {
               vm.ExportFolderPath = folders[0].Path.LocalPath;
               vm.ExportFolderDisplay = folders[0].Path.LocalPath;
            }
         }
      }
   }
}