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
         //Auto-scroll logic for Deep-Linking from Dashboard
         if (e.PropertyName == nameof(GradebookViewModel.SelectedGradebookRow) && sender is GradebookViewModel scrollVm)
         {
            if (scrollVm.SelectedGradebookRow != null)
            {
               var grid = this.FindControl<DataGrid>("RosterGrid");
               // Pushes the scroll action to the UI thread to ensure the grid has rendered the row first
               Avalonia.Threading.Dispatcher.UIThread.Post(() => 
               {
                  grid?.ScrollIntoView(scrollVm.SelectedGradebookRow, null);
               }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
         }
      }

      private void GenerateDynamicColumns(GradebookViewModel vm)
      {
         var grid = this.FindControl<DataGrid>("RosterGrid");
         if (grid == null) return;

         // Preserve the highlighted student row before unbinding ItemsSource!
         var preservedSelection = vm.SelectedGradebookRow;
         var backupData = grid.ItemsSource;
         grid.ItemsSource = null;

         try
         {
            // 1. Find the fixed columns safely (independent of pixel width)
            var colCheckbox = grid.Columns.FirstOrDefault(c => !c.CanUserSort && string.IsNullOrEmpty(c.SortMemberPath)); 
            var colLRN = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "StudentID");
            var colFirstName = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "StudentInfo.FirstName");
            var colLastName = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "StudentInfo.LastName");
            var colFinalGrade = grid.Columns.FirstOrDefault(c => c.SortMemberPath == "FinalGradeNumeric");

            // 2. Toggle visibility
            if (colLRN != null) colLRN.IsVisible = vm.ShowStudentId;
            if (colFirstName != null) colFirstName.IsVisible = vm.ShowFirstName;
            if (colLastName != null) colLastName.IsVisible = vm.ShowLastName;
            
            if (colFinalGrade != null) 
            {
               colFinalGrade.IsVisible = vm.ShowFinalGrade;
               colFinalGrade.Header = vm.DynamicFinalColumnName;
            }

            // 3. Dynamically calculate FrozenColumnCount so hidden identity columns never freeze quiz columns!
            int frozenCount = 1; // Checkbox column
            if (vm.ShowStudentId) frozenCount++;
            if (vm.ShowLastName) frozenCount++;
            if (vm.ShowFirstName) frozenCount++;
            grid.FrozenColumnCount = frozenCount;

            // 4. Keep ONLY our 5 fixed static columns
            var staticColumns = new System.Collections.Generic.List<Avalonia.Controls.DataGridColumn>();
            if (colCheckbox != null) staticColumns.Add(colCheckbox); 
            if (colLRN != null) staticColumns.Add(colLRN);
            if (colFirstName != null) staticColumns.Add(colFirstName);
            if (colLastName != null) staticColumns.Add(colLastName);
            if (colFinalGrade != null) staticColumns.Add(colFinalGrade);

            var columnsToRemove = grid.Columns.Where(c => !staticColumns.Contains(c)).ToList();
            foreach (var col in columnsToRemove)
            {
               grid.Columns.Remove(col);
            }

            // 5. Insert new dynamic columns right before the Final Grade column
            int insertIndex = colFinalGrade != null ? grid.Columns.IndexOf(colFinalGrade) : grid.Columns.Count - 1;
            if (insertIndex < 0) insertIndex = grid.Columns.Count;
            
            // MODE A: SEMESTER AVERAGE 
            if (vm.IsSemesterAverageView)
            {
               var midColumn = new DataGridTemplateColumn { Header = "Midterm", IsVisible = vm.ShowMidtermGrade, CanUserSort = true, SortMemberPath = "MidtermGradeNumeric", IsReadOnly = true, CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) => { 
               var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
               tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding("MidtermGradeDisplay"));
               tb.Bind(ToolTip.TipProperty, new Avalonia.Data.Binding("MidtermComputationTooltip")); 
               return tb; })};
               grid.Columns.Insert(insertIndex++, midColumn);

               var finalColumn = new DataGridTemplateColumn { Header = "Final", IsVisible = vm.ShowFinalTermGrade, CanUserSort = true, SortMemberPath = "FinalTermGradeNumeric", IsReadOnly = true, CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) => { 
               var tb2 = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
               tb2.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding("FinalTermGradeDisplay"));
               tb2.Bind(ToolTip.TipProperty, new Avalonia.Data.Binding("FinalComputationTooltip")); 
               return tb2; })};
               grid.Columns.Insert(insertIndex, finalColumn);
               
               return; 
            }

            // MODE B: MIDTERM or FINAL
            foreach (var category in vm.CategoryFilters)
            {
               if (!category.IsCategoryVisible) continue;

               var dbCategory = vm.AvailableCategories.FirstOrDefault(c =>
                   string.Equals((c.Name ?? string.Empty).Trim(), (category.CategoryName ?? string.Empty).Trim(), System.StringComparison.OrdinalIgnoreCase));
               category.SequenceOrder = dbCategory != null ? dbCategory.SequenceOrder : 0;

               foreach (var filter in category.Assessments)
               {
                  if (!filter.IsVisible) continue; 

                  var assessment = filter.DbModel;
                  if (!string.Equals((assessment.GradingPeriod ?? string.Empty).Trim(), (vm.SelectedTermView ?? string.Empty).Trim(), System.StringComparison.OrdinalIgnoreCase)) continue;

                  var headerPanel = new Avalonia.Controls.StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 4) };
                  
                  var titleTb = new Avalonia.Controls.TextBlock { Text = assessment.Title, FontWeight = Avalonia.Media.FontWeight.Bold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                  titleTb[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMainBrush");
                  headerPanel.Children.Add(titleTb);

                  var catTb = new Avalonia.Controls.TextBlock { Text = $"[{assessment.Category}]", FontSize = 11, FontWeight = Avalonia.Media.FontWeight.SemiBold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                  catTb[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("InfoBrush");
                  headerPanel.Children.Add(catTb);

                  var maxTb = new Avalonia.Controls.TextBlock { Text = $"Max: {assessment.MaxScore}", FontSize = 11, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                  maxTb[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMutedBrush");
                  headerPanel.Children.Add(maxTb);

                  var buttonPanel = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 4, 0, 0) };
                  
                  var editIcon = new Avalonia.Controls.TextBlock { FontSize = 15 };
                  editIcon.Classes.Add("Icon");
                  editIcon[!Avalonia.Controls.TextBlock.TextProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("IconEdit");
                  editIcon[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMainBrush");

                  var delIcon = new Avalonia.Controls.TextBlock { FontSize = 15 };
                  delIcon.Classes.Add("Icon");
                  delIcon[!Avalonia.Controls.TextBlock.TextProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("IconDelete");
                  delIcon[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("DangerBrush");

                  var editBtn = new Avalonia.Controls.Button { Content = editIcon, Background = Avalonia.Media.Brushes.Transparent, Command = vm.EditAssessmentCommand, CommandParameter = assessment, Padding = new Avalonia.Thickness(4) };
                  var delBtn = new Avalonia.Controls.Button { Content = delIcon, Background = Avalonia.Media.Brushes.Transparent, Command = vm.DeleteAssessmentCommand, CommandParameter = assessment, Padding = new Avalonia.Thickness(4) };
                  
                  buttonPanel.Children.Add(editBtn);
                  buttonPanel.Children.Add(delBtn);
                  headerPanel.Children.Add(buttonPanel);

                  bool isGroupAssessment = assessment.AssessmentType == "Group/Pair";
                  var newColumn = new DataGridTemplateColumn
                  {
                     Header = headerPanel,
                     Width = DataGridLength.Auto,
                     MaxWidth = 250,
                     CanUserSort = true,
                     SortMemberPath = $"Scores[{assessment.AssessmentID}].PointsEarned",
                     IsReadOnly = isGroupAssessment, 

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
                        box.AttachedToVisualTree += (sender, args) =>
                        {
                           Avalonia.Threading.Dispatcher.UIThread.Post(() => { box.SelectAll(); }, Avalonia.Threading.DispatcherPriority.Input);
                        };
                        return box;
                     })
                  };

                  grid.Columns.Insert(insertIndex, newColumn);
                  insertIndex++;
               }

               var periodAssessments = vm.ClassAssessments
                   .Where(a => string.Equals((a.Category ?? string.Empty).Trim(), (category.CategoryName ?? string.Empty).Trim(), System.StringComparison.OrdinalIgnoreCase)
                       && string.Equals((a.GradingPeriod ?? string.Empty).Trim(), (vm.SelectedTermView ?? string.Empty).Trim(), System.StringComparison.OrdinalIgnoreCase))
                   .ToList();
                   
               if (periodAssessments.Any())
               {
                  string rawKey = $"{vm.SelectedTermView}_{category.CategoryName}";
                  string safeKey = GradebookViewModel.GenerateSafeKey(rawKey);

                  // Create TS Column (Bound to centralized TsColumnBrush)
                  var tsHeader = new Avalonia.Controls.TextBlock { Text = "TS", FontWeight = Avalonia.Media.FontWeight.Bold };
                  tsHeader[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TsColumnBrush");
                  Avalonia.Controls.ToolTip.SetTip(tsHeader, $"Transmuted Score for {category.CategoryName}");
                  
                  var tsColumn = new DataGridTemplateColumn { Header = tsHeader, IsReadOnly = true, CanUserSort = false, Width = DataGridLength.Auto };
                  tsColumn.CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                  {
                     var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, FontWeight = Avalonia.Media.FontWeight.SemiBold };
                     tb[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TsColumnBrush");
                     tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding($"CategoryGrades[{safeKey}].TsDisplay"));
                     tb.Bind(Avalonia.Controls.ToolTip.TipProperty, new Avalonia.Data.Binding($"CategoryGrades[{safeKey}].TsTooltip"));
                     return tb;
                  });
                  grid.Columns.Insert(insertIndex++, tsColumn);

                  // Create WS Column (Bound to centralized WsColumnBrush)
                  var wsHeader = new Avalonia.Controls.TextBlock { Text = "WS", FontWeight = Avalonia.Media.FontWeight.Bold };
                  wsHeader[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("WsColumnBrush");
                  Avalonia.Controls.ToolTip.SetTip(wsHeader, $"Weighted Score for {category.CategoryName}");

                  var wsColumn = new DataGridTemplateColumn { Header = wsHeader, IsReadOnly = true, CanUserSort = false, Width = DataGridLength.Auto };
                  wsColumn.CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, __) =>
                  {
                     var tb = new Avalonia.Controls.TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, FontWeight = Avalonia.Media.FontWeight.Bold };
                     tb[!Avalonia.Controls.TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("WsColumnBrush");
                     tb.Bind(Avalonia.Controls.TextBlock.TextProperty, new Avalonia.Data.Binding($"CategoryGrades[{safeKey}].WsDisplay"));
                     tb.Bind(Avalonia.Controls.ToolTip.TipProperty, new Avalonia.Data.Binding($"CategoryGrades[{safeKey}].WsTooltip"));
                     return tb;
                  });
                  grid.Columns.Insert(insertIndex++, wsColumn);
               }
            }
         }
         finally
         {
            grid.ItemsSource = backupData;

            if (preservedSelection != null)
            {
               vm.SelectedGradebookRow = preservedSelection;
               grid.SelectedItem = preservedSelection;
               Avalonia.Threading.Dispatcher.UIThread.Post(() =>
               {
                  grid.ScrollIntoView(preservedSelection, null);
               }, Avalonia.Threading.DispatcherPriority.Loaded);
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
         try 
         {
             var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
             if (topLevel == null) return;

             var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
             {
                Title = "Select Export Destination", AllowMultiple = false
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
         catch { /* Silently ignore OS dialog cancellations or access errors */ }
      }
   }
}