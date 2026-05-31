using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels
{
    public partial class DirectoryViewModel : ViewModelBase
    {
        [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;
        
        private List<StudentRowViewModel> _allStudents = new();
        [ObservableProperty] public partial ObservableCollection<StudentRowViewModel> DisplayedStudents { get; set; } = new();

        // --- Column Visibility Toggles ---
        [ObservableProperty] public partial bool ShowLrnColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowLastNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowFirstNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowMiddleNameColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowSuffixColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowGenderColumn { get; set; } = true;

        // NEW Column Toggles
        [ObservableProperty] public partial bool ShowGradeYearLevelColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowSectionBlockColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowEnrollmentStatusColumn { get; set; } = true;

        // --- Add Student Form Properties ---
        [ObservableProperty] public partial bool IsAddingStudent { get; set; } = false;
        [ObservableProperty] public partial string NewStudentLrn { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentFirstName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentMiddleName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentLastName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentSuffix { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentGender { get; set; } = "Male";

        // NEW Form Properties
        [ObservableProperty] public partial string NewStudentGradeYearLevel { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentSectionBlock { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentEnrollmentStatus { get; set; } = "Regular";

        public IRelayCommand ToggleAddStudentFormCommand { get; }
        public IRelayCommand SaveStudentCommand { get; }
        public IRelayCommand<StudentRowViewModel> EditOrSaveStudentCommand { get; } // UPDATED
        public IRelayCommand<StudentRowViewModel> ViewProfileCommand { get; }
        public IRelayCommand<StudentRowViewModel> DeleteStudentCommand { get; }

        public DirectoryViewModel()
        {
            ToggleAddStudentFormCommand = new RelayCommand(() => IsAddingStudent = !IsAddingStudent);
            SaveStudentCommand = new RelayCommand(SaveStudent);
            EditOrSaveStudentCommand = new RelayCommand<StudentRowViewModel>(EditOrSaveStudent!);
            ViewProfileCommand = new RelayCommand<StudentRowViewModel>(ViewProfile!);
            DeleteStudentCommand = new RelayCommand<StudentRowViewModel>(DeleteStudent!);

            LoadStudents();
        }

        private async void LoadStudents()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var rawStudents = await db.Table<Centriku.Models.Student>().ToListAsync();
            
            // Wrap the raw database models in our UI wrapper
            _allStudents = [.. rawStudents.Where(s => !s.IsArchived).Select(s => new StudentRowViewModel(s))];
            UpdateDisplayedStudents();
        }

        private async void SaveStudent()
        {
            if (string.IsNullOrWhiteSpace(NewStudentLrn) || string.IsNullOrWhiteSpace(NewStudentLastName))
                return;

            var db = new Centriku.Services.DatabaseService().GetConnection();
            var newStudent = new Centriku.Models.Student
            {
                StudentID = NewStudentLrn, 
                FirstName = NewStudentFirstName,
                MiddleName = NewStudentMiddleName,
                LastName = NewStudentLastName,
                Suffix = NewStudentSuffix,
                Gender = NewStudentGender,
                GradeYearLevel = NewStudentGradeYearLevel,
                SectionBlock = NewStudentSectionBlock,
                EnrollmentStatus = NewStudentEnrollmentStatus,
                IsArchived = false
            };

            await db.InsertOrReplaceAsync(newStudent);
            
            NewStudentLrn = string.Empty;
            NewStudentFirstName = string.Empty;
            NewStudentMiddleName = string.Empty;
            NewStudentLastName = string.Empty;
            NewStudentSuffix = string.Empty;
            NewStudentGradeYearLevel = string.Empty;
            NewStudentSectionBlock = string.Empty;
            NewStudentEnrollmentStatus = "Regular";
            
            IsAddingStudent = false;
            LoadStudents();
        }

        private async void EditOrSaveStudent(StudentRowViewModel row)
        {
            if (row == null) return;

            if (!row.IsEditing)
            {
                row.IsEditing = true;
            }
            else
            {
                var db = new Centriku.Services.DatabaseService().GetConnection();
                await db.UpdateAsync(row.DbModel);
                row.IsEditing = false;
            }
        }

        private async void DeleteStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            row.DbModel.IsArchived = true;
            await db.UpdateAsync(row.DbModel);
            LoadStudents();
        }

        partial void OnSearchQueryChanged(string value)
        {
            UpdateDisplayedStudents();
        }

        private void UpdateDisplayedStudents()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                DisplayedStudents = new ObservableCollection<StudentRowViewModel>(_allStudents);
                return;
            }
            var lowerQuery = SearchQuery.ToLower();
            var filtered = _allStudents.Where(s => 
                (s.StudentID?.Contains(lowerQuery) == true) || 
                (s.LastName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) || 
                (s.FirstName?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (s.GradeYearLevel?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true) ||
                (s.SectionBlock?.ToLower().Contains(lowerQuery, StringComparison.CurrentCultureIgnoreCase) == true));

            DisplayedStudents = new ObservableCollection<StudentRowViewModel>(filtered);
        }

        private void ViewProfile(StudentRowViewModel row)
        {
            System.Console.WriteLine($"Viewing Profile for {row.FirstName} {row.LastName}");
        }
    }
    public partial class StudentRowViewModel(Centriku.Models.Student student) : ObservableObject
    {
      public Centriku.Models.Student DbModel { get; } = student;

      [ObservableProperty]
      public partial bool IsEditing { get; set; } = false; // Rows are locked by default

      public string StudentID
        {
            get => DbModel.StudentID ?? string.Empty;
            set { DbModel.StudentID = value; OnPropertyChanged(); }
        }
        public string FirstName
        {
            get => DbModel.FirstName ?? string.Empty;
            set { DbModel.FirstName = value; OnPropertyChanged(); }
        }
        public string LastName
        {
            get => DbModel.LastName ?? string.Empty;
            set { DbModel.LastName = value; OnPropertyChanged(); }
        }
        public string MiddleName
        {
            get => DbModel.MiddleName ?? string.Empty;
            set { DbModel.MiddleName = value; OnPropertyChanged(); }
        }
        public string Suffix
        {
            get => DbModel.Suffix ?? string.Empty;
            set { DbModel.Suffix = value; OnPropertyChanged(); }
        }
        public string Gender
        {
            get => DbModel.Gender ?? string.Empty;
            set { DbModel.Gender = value; OnPropertyChanged(); }
        }
        public string GradeYearLevel
        {
            get => DbModel.GradeYearLevel ?? string.Empty;
            set { DbModel.GradeYearLevel = value; OnPropertyChanged(); }
        }
        public string SectionBlock
        {
            get => DbModel.SectionBlock ?? string.Empty;
            set { DbModel.SectionBlock = value; OnPropertyChanged(); }
        }
        public string EnrollmentStatus
        {
            get => DbModel.EnrollmentStatus ?? string.Empty;
            set { DbModel.EnrollmentStatus = value; OnPropertyChanged(); }
        }
   }
}