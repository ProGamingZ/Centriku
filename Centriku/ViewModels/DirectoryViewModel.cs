using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels
{
    // sets up the class, the commands, and calls the initialization tasks.
    public partial class DirectoryViewModel : ViewModelBase
    {
        [ObservableProperty] public partial int SelectedTabIndex { get; set; } = 0;
        public static event System.Action? OnStudentRosterChanged;
        // Roster Commands
        public IRelayCommand ToggleAddStudentFormCommand { get; }
        public IRelayCommand SaveStudentCommand { get; }
        public IRelayCommand<StudentRowViewModel> EditOrSaveStudentCommand { get; }
        public IRelayCommand<StudentRowViewModel> ArchiveStudentCommand { get; }
        public IRelayCommand<StudentRowViewModel> RestoreStudentCommand { get; } 
        public IRelayCommand<StudentRowViewModel> DeleteStudentCommand { get; } 
        
        // Profile Commands
        public IRelayCommand<StudentRowViewModel> ViewProfileCommand { get; }
        public IRelayCommand CloseProfileCommand { get; }

        public DirectoryViewModel()
        {
            ToggleAddStudentFormCommand = new RelayCommand(() => IsAddingStudent = !IsAddingStudent);
            SaveStudentCommand = new RelayCommand(SaveStudent);
            EditOrSaveStudentCommand = new RelayCommand<StudentRowViewModel>(EditOrSaveStudent!);
            ViewProfileCommand = new RelayCommand<StudentRowViewModel>(ViewProfile!);
            
            ArchiveStudentCommand = new RelayCommand<StudentRowViewModel>(ArchiveStudent!); 
            RestoreStudentCommand = new RelayCommand<StudentRowViewModel>(RestoreStudent!); 
            DeleteStudentCommand = new RelayCommand<StudentRowViewModel>(DeleteStudent!);
            CloseProfileCommand = new RelayCommand(() => IsProfileOpen = false); 

            LoadStudents();
        }
    }
}