using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Services;
using System.Threading.Tasks;

namespace Centriku.ViewModels
{
    public partial class DirectoryViewModel : ViewModelBase
    {
        private readonly IDatabaseService _dbService;

        [ObservableProperty] public partial int SelectedTabIndex { get; set; } = 0;
        public static event System.Action? OnStudentRosterChanged;
        
        // Synchronous Commands (No database hits)
        public IRelayCommand ToggleAddStudentFormCommand { get; }
        public IRelayCommand CloseProfileCommand { get; }

        // Asynchronous Commands (Hits the database)
        public IAsyncRelayCommand SaveStudentCommand { get; }
        public IAsyncRelayCommand<StudentRowViewModel> EditOrSaveStudentCommand { get; }
        public IAsyncRelayCommand<StudentRowViewModel> ArchiveStudentCommand { get; }
        public IAsyncRelayCommand<StudentRowViewModel> RestoreStudentCommand { get; } 
        public IAsyncRelayCommand<StudentRowViewModel> DeleteStudentCommand { get; } 
        public IAsyncRelayCommand<StudentRowViewModel> ViewProfileCommand { get; }

        public DirectoryViewModel(IDatabaseService dbService)
        {
            _dbService = dbService;
            
            ToggleAddStudentFormCommand = new RelayCommand(() => IsAddingStudent = !IsAddingStudent);
            CloseProfileCommand = new RelayCommand(() => IsProfileOpen = false); 

            SaveStudentCommand = new AsyncRelayCommand(SaveStudentAsync);
            EditOrSaveStudentCommand = new AsyncRelayCommand<StudentRowViewModel>(EditOrSaveStudentAsync!);
            ViewProfileCommand = new AsyncRelayCommand<StudentRowViewModel>(ViewProfileAsync!);
            ArchiveStudentCommand = new AsyncRelayCommand<StudentRowViewModel>(ArchiveStudentAsync!); 
            RestoreStudentCommand = new AsyncRelayCommand<StudentRowViewModel>(RestoreStudentAsync!); 
            DeleteStudentCommand = new AsyncRelayCommand<StudentRowViewModel>(DeleteStudentAsync!);

            // Fire-and-forget for the constructor boot sequence
            _ = LoadStudentsAsync();
        }
    }
}