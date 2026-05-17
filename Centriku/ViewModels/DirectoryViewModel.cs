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
        
        // Notice we are now using our new Wrapper Class here
        private List<StudentRowViewModel> _allStudents = new();
        [ObservableProperty] public partial ObservableCollection<StudentRowViewModel> DisplayedStudents { get; set; } = new();

        // --- Column Visibility Toggles ---
        [ObservableProperty] public partial bool ShowLrnColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowLastNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowFirstNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowMiddleNameColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowSuffixColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowGenderColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowDobColumn { get; set; } = false;

        // --- Add Student Form Properties ---
        [ObservableProperty] public partial bool IsAddingStudent { get; set; } = false;
        [ObservableProperty] public partial string NewStudentLrn { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentFirstName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentMiddleName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentLastName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentSuffix { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewStudentGender { get; set; } = "Male";
        [ObservableProperty] public partial DateTimeOffset? NewStudentDob { get; set; } = DateTimeOffset.Now; 

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
            _allStudents = rawStudents.Select(s => new StudentRowViewModel(s)).ToList();
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
                DateOfBirth = NewStudentDob?.DateTime ?? DateTime.Now 
            };

            await db.InsertOrReplaceAsync(newStudent);
            
            NewStudentLrn = string.Empty;
            NewStudentFirstName = string.Empty;
            NewStudentMiddleName = string.Empty;
            NewStudentLastName = string.Empty;
            NewStudentSuffix = string.Empty;
            NewStudentDob = DateTimeOffset.Now;
            IsAddingStudent = false;
            
            LoadStudents();
        }

        // --- NEW: Toggles the row between Edit Mode and Read Mode ---
        private async void EditOrSaveStudent(StudentRowViewModel row)
        {
            if (row == null) return;

            if (!row.IsEditing)
            {
                // User clicked "Edit". Unlock the row inputs!
                row.IsEditing = true;
            }
            else
            {
                // User clicked "Save". Lock the row and update the Database!
                var db = new Centriku.Services.DatabaseService().GetConnection();
                await db.UpdateAsync(row.DbModel);
                row.IsEditing = false;
                System.Console.WriteLine($"Inline Edit Saved for LRN: {row.StudentID}");
            }
        }

        private async void DeleteStudent(StudentRowViewModel row)
        {
            if (row == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.DeleteAsync(row.DbModel);
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
                (s.LastName?.ToLower().Contains(lowerQuery) == true) || 
                (s.FirstName?.ToLower().Contains(lowerQuery) == true));

            DisplayedStudents = new ObservableCollection<StudentRowViewModel>(filtered);
        }

        private void ViewProfile(StudentRowViewModel row)
        {
            System.Console.WriteLine($"Viewing Profile for {row.FirstName} {row.LastName}");
        }
    }
    public partial class StudentRowViewModel : ObservableObject
    {
        public Centriku.Models.Student DbModel { get; }

        [ObservableProperty]
        public partial bool IsEditing { get; set; }

        // We wrap the properties so the UI instantly detects changes when typing
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
        public DateTime DateOfBirth
        {
            get => DbModel.DateOfBirth;
            set { DbModel.DateOfBirth = value; OnPropertyChanged(); }
        }

        public StudentRowViewModel(Centriku.Models.Student student)
        {
            DbModel = student;
            IsEditing = false; // Rows are locked by default
        }
    }
}