using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;

namespace Centriku.ViewModels.Settings
{
   // Holds the raw data for the dynamic Preview DataGrid
   public class PreviewStudentRow
   {
      public string Col1Text { get; set; } = ""; public string Col2Text { get; set; } = "";
      public string Col3Text { get; set; } = ""; public string Col4Text { get; set; } = "";
      public string Col5Text { get; set; } = ""; public string Col6Text { get; set; } = "";
      public string Col7Text { get; set; } = ""; public string Col8Text { get; set; } = "";
      public string Col9Text { get; set; } = "";
   }

   public partial class ImportSettingsViewModel : ViewModelBase
   {
      [ObservableProperty] public partial AppSettings CurrentSettings { get; set; } = new();
      [ObservableProperty] public partial bool IsSaving { get; set; } = false;

      private readonly string[] _allFields = ["LRN", "Last Name", "First Name", "Middle Name", "Suffix", "Gender", "Grade/Year", "Section/Block", "Status"];
      
      // Genders restricted to just Male/Female
      public ObservableCollection<string> AvailableGenders { get; } = ["Male", "Female"];
      public ObservableCollection<string> AvailableStatuses { get; } = ["Regular", "Irregular", "Transferee"];

      [ObservableProperty] public partial ObservableCollection<string> Col1Options { get; set; } = new();
      [ObservableProperty] public partial ObservableCollection<string> Col2Options { get; set; } = new();
      [ObservableProperty] public partial ObservableCollection<string> Col3Options { get; set; } = new();
      [ObservableProperty] public partial ObservableCollection<string> Col4Options { get; set; } = new();
      [ObservableProperty] public partial ObservableCollection<string> Col5Options { get; set; } = new();
      [ObservableProperty] public partial ObservableCollection<string> Col6Options { get; set; } = new();
      [ObservableProperty] public partial ObservableCollection<string> Col7Options { get; set; } = new();
      [ObservableProperty] public partial ObservableCollection<string> Col8Options { get; set; } = new();
      [ObservableProperty] public partial ObservableCollection<string> Col9Options { get; set; } = new();

      private bool _isUpdating = false;

      private string _col1Selected = "Ignore"; public string Col1Selected { get => _col1Selected; set { if (value == null) return; SetProperty(ref _col1Selected, value); if (!_isUpdating) UpdateMappingState(); } }
      private string _col2Selected = "Ignore"; public string Col2Selected { get => _col2Selected; set { if (value == null) return; SetProperty(ref _col2Selected, value); if (!_isUpdating) UpdateMappingState(); } }
      private string _col3Selected = "Ignore"; public string Col3Selected { get => _col3Selected; set { if (value == null) return; SetProperty(ref _col3Selected, value); if (!_isUpdating) UpdateMappingState(); } }
      private string _col4Selected = "Ignore"; public string Col4Selected { get => _col4Selected; set { if (value == null) return; SetProperty(ref _col4Selected, value); if (!_isUpdating) UpdateMappingState(); } }
      private string _col5Selected = "Ignore"; public string Col5Selected { get => _col5Selected; set { if (value == null) return; SetProperty(ref _col5Selected, value); if (!_isUpdating) UpdateMappingState(); } }
      private string _col6Selected = "Ignore"; public string Col6Selected { get => _col6Selected; set { if (value == null) return; SetProperty(ref _col6Selected, value); if (!_isUpdating) UpdateMappingState(); } }
      private string _col7Selected = "Ignore"; public string Col7Selected { get => _col7Selected; set { if (value == null) return; SetProperty(ref _col7Selected, value); if (!_isUpdating) UpdateMappingState(); } }
      private string _col8Selected = "Ignore"; public string Col8Selected { get => _col8Selected; set { if (value == null) return; SetProperty(ref _col8Selected, value); if (!_isUpdating) UpdateMappingState(); } }
      private string _col9Selected = "Ignore"; public string Col9Selected { get => _col9Selected; set { if (value == null) return; SetProperty(ref _col9Selected, value); if (!_isUpdating) UpdateMappingState(); } }

      [ObservableProperty] public partial ObservableCollection<PreviewStudentRow> PreviewRows { get; set; } = new();
      
      [ObservableProperty] public partial string Col1Header { get; set; } = ""; [ObservableProperty] public partial bool Col1Visible { get; set; } = false;
      [ObservableProperty] public partial string Col2Header { get; set; } = ""; [ObservableProperty] public partial bool Col2Visible { get; set; } = false;
      [ObservableProperty] public partial string Col3Header { get; set; } = ""; [ObservableProperty] public partial bool Col3Visible { get; set; } = false;
      [ObservableProperty] public partial string Col4Header { get; set; } = ""; [ObservableProperty] public partial bool Col4Visible { get; set; } = false;
      [ObservableProperty] public partial string Col5Header { get; set; } = ""; [ObservableProperty] public partial bool Col5Visible { get; set; } = false;
      [ObservableProperty] public partial string Col6Header { get; set; } = ""; [ObservableProperty] public partial bool Col6Visible { get; set; } = false;
      [ObservableProperty] public partial string Col7Header { get; set; } = ""; [ObservableProperty] public partial bool Col7Visible { get; set; } = false;
      [ObservableProperty] public partial string Col8Header { get; set; } = ""; [ObservableProperty] public partial bool Col8Visible { get; set; } = false;
      [ObservableProperty] public partial string Col9Header { get; set; } = ""; [ObservableProperty] public partial bool Col9Visible { get; set; } = false;

      public string DefaultGender { get => CurrentSettings.DefaultGender; set { CurrentSettings.DefaultGender = value; OnPropertyChanged(); if (!_isUpdating) UpdateMappingState(); } }
      public string DefaultGradeLevel { get => CurrentSettings.DefaultGradeLevel; set { CurrentSettings.DefaultGradeLevel = value; OnPropertyChanged(); if (!_isUpdating) UpdateMappingState(); } }
      public string DefaultSection { get => CurrentSettings.DefaultSection; set { CurrentSettings.DefaultSection = value; OnPropertyChanged(); if (!_isUpdating) UpdateMappingState(); } }
      public string DefaultEnrollmentStatus { get => CurrentSettings.DefaultEnrollmentStatus; set { CurrentSettings.DefaultEnrollmentStatus = value; OnPropertyChanged(); if (!_isUpdating) UpdateMappingState(); } }

      public IRelayCommand SaveSettingsCommand { get; }

      public ImportSettingsViewModel()
      {
         SaveSettingsCommand = new RelayCommand(SaveSettings);
         LoadSettings();
      }

      private async void LoadSettings()
      {
         var db = new Centriku.Services.DatabaseService().GetConnection();
         await db.CreateTableAsync<AppSettings>(); 
         var savedSettings = await db.Table<AppSettings>().FirstOrDefaultAsync();
         if (savedSettings != null) CurrentSettings = savedSettings;
         else { CurrentSettings = new AppSettings(); await db.InsertAsync(CurrentSettings); }

         // Fix old settings if they previously had "Unspecified" saved
         if (CurrentSettings.DefaultGender == "Unspecified")
         {
            CurrentSettings.DefaultGender = "Male";
            await db.UpdateAsync(CurrentSettings);
         }

         _isUpdating = true;
         void SetCol(int dbIndex, string field) {
            if (dbIndex == 0) Col1Selected = field; else if (dbIndex == 1) Col2Selected = field;
            else if (dbIndex == 2) Col3Selected = field; else if (dbIndex == 3) Col4Selected = field;
            else if (dbIndex == 4) Col5Selected = field; else if (dbIndex == 5) Col6Selected = field;
            else if (dbIndex == 6) Col7Selected = field; else if (dbIndex == 7) Col8Selected = field;
            else if (dbIndex == 8) Col9Selected = field; 
         }

         SetCol(CurrentSettings.LrnColumnIndex, "LRN"); SetCol(CurrentSettings.LastNameColumnIndex, "Last Name");
         SetCol(CurrentSettings.FirstNameColumnIndex, "First Name"); SetCol(CurrentSettings.MiddleNameColumnIndex, "Middle Name");
         SetCol(CurrentSettings.SuffixColumnIndex, "Suffix"); SetCol(CurrentSettings.GenderColumnIndex, "Gender");
         SetCol(CurrentSettings.GradeLevelColumnIndex, "Grade/Year"); SetCol(CurrentSettings.SectionColumnIndex, "Section/Block");
         SetCol(CurrentSettings.EnrollmentStatusColumnIndex, "Status");

         OnPropertyChanged(nameof(DefaultGender)); OnPropertyChanged(nameof(DefaultGradeLevel));
         OnPropertyChanged(nameof(DefaultSection)); OnPropertyChanged(nameof(DefaultEnrollmentStatus));
         
         _isUpdating = false;
         UpdateMappingState();
      }

      private void UpdateMappingState()
      {
         _isUpdating = true;

         var selected = new List<string> { _col1Selected, _col2Selected, _col3Selected, _col4Selected, _col5Selected, _col6Selected, _col7Selected, _col8Selected, _col9Selected };
         var available = _allFields.Where(f => !selected.Contains(f)).ToList();

         // 1. SAFELY REBUILD LISTS IN-PLACE
         void RefreshOptions(ObservableCollection<string> options, string currentSelection)
         {
               options.Clear();
               options.Add("Ignore");
               
               if (currentSelection != "Ignore" && !string.IsNullOrEmpty(currentSelection))
               {
                  options.Add(currentSelection);
               }
               
               foreach (var a in available)
               {
                  options.Add(a);
               }
         }

         RefreshOptions(Col1Options, _col1Selected); RefreshOptions(Col2Options, _col2Selected);
         RefreshOptions(Col3Options, _col3Selected); RefreshOptions(Col4Options, _col4Selected);
         RefreshOptions(Col5Options, _col5Selected); RefreshOptions(Col6Options, _col6Selected);
         RefreshOptions(Col7Options, _col7Selected); RefreshOptions(Col8Options, _col8Selected);
         RefreshOptions(Col9Options, _col9Selected); 

         // 2. SAVE SETTINGS TO DATABASE VARIABLES
         int GetColIndex(string field) => selected.IndexOf(field);
         CurrentSettings.LrnColumnIndex = GetColIndex("LRN"); CurrentSettings.LastNameColumnIndex = GetColIndex("Last Name");
         CurrentSettings.FirstNameColumnIndex = GetColIndex("First Name"); CurrentSettings.MiddleNameColumnIndex = GetColIndex("Middle Name");
         CurrentSettings.SuffixColumnIndex = GetColIndex("Suffix"); CurrentSettings.GenderColumnIndex = GetColIndex("Gender");
         CurrentSettings.GradeLevelColumnIndex = GetColIndex("Grade/Year"); CurrentSettings.SectionColumnIndex = GetColIndex("Section/Block");
         CurrentSettings.EnrollmentStatusColumnIndex = GetColIndex("Status");

         // 3. REBUILD PREVIEW DATAGRID COLUMNS
         Col1Header = _col1Selected; Col1Visible = _col1Selected != "Ignore";
         Col2Header = _col2Selected; Col2Visible = _col2Selected != "Ignore";
         Col3Header = _col3Selected; Col3Visible = _col3Selected != "Ignore";
         Col4Header = _col4Selected; Col4Visible = _col4Selected != "Ignore";
         Col5Header = _col5Selected; Col5Visible = _col5Selected != "Ignore";
         Col6Header = _col6Selected; Col6Visible = _col6Selected != "Ignore";
         Col7Header = _col7Selected; Col7Visible = _col7Selected != "Ignore";
         Col8Header = _col8Selected; Col8Visible = _col8Selected != "Ignore";
         Col9Header = _col9Selected; Col9Visible = _col9Selected != "Ignore";

         // 4. GENERATE 1 SINGLE PREVIEW ROW
         string GetPreview(string field)
         {
               if (field == "Gender") return DefaultGender;
               if (field == "Grade/Year") return DefaultGradeLevel;
               if (field == "Section/Block") return DefaultSection;
               if (field == "Status") return DefaultEnrollmentStatus;
               return "--"; 
         }

         var singleRow = new PreviewStudentRow {
               Col1Text = GetPreview(_col1Selected), Col2Text = GetPreview(_col2Selected), Col3Text = GetPreview(_col3Selected),
               Col4Text = GetPreview(_col4Selected), Col5Text = GetPreview(_col5Selected), Col6Text = GetPreview(_col6Selected),
               Col7Text = GetPreview(_col7Selected), Col8Text = GetPreview(_col8Selected), Col9Text = GetPreview(_col9Selected)
         };
         
         PreviewRows = new ObservableCollection<PreviewStudentRow> { singleRow };

         void ForceUpdate(ref string backingField, string propName)
         {
               string savedValue = backingField;
               backingField = null!;           // 1. Temporarily clear the backend value
               OnPropertyChanged(propName);    // 2. Tell UI it's cleared
               backingField = savedValue;      // 3. Put the correct value back
               OnPropertyChanged(propName);    // 4. Tell UI it's back 
         }

         ForceUpdate(ref _col1Selected, nameof(Col1Selected));
         ForceUpdate(ref _col2Selected, nameof(Col2Selected));
         ForceUpdate(ref _col3Selected, nameof(Col3Selected));
         ForceUpdate(ref _col4Selected, nameof(Col4Selected));
         ForceUpdate(ref _col5Selected, nameof(Col5Selected));
         ForceUpdate(ref _col6Selected, nameof(Col6Selected));
         ForceUpdate(ref _col7Selected, nameof(Col7Selected));
         ForceUpdate(ref _col8Selected, nameof(Col8Selected));
         ForceUpdate(ref _col9Selected, nameof(Col9Selected));

         _isUpdating = false;
      }

      private async void SaveSettings()
      {
         IsSaving = true;
         var db = new Centriku.Services.DatabaseService().GetConnection();
         await db.UpdateAsync(CurrentSettings);
         await Task.Delay(600); 
         IsSaving = false;
      }
   }
}