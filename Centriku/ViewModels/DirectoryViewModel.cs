using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels
{
    public partial class DirectoryViewModel : ViewModelBase
    {
        // This will bind to the Search Box
        [ObservableProperty]
        public partial string SearchQuery { get; set; } = string.Empty;

        // The master list of all students
        [ObservableProperty]
        public partial ObservableCollection<StudentProfile> Students { get; set; }

        // We declare the command explicitly to avoid the XAML ghost error
        public IRelayCommand<StudentProfile> ViewProfileCommand { get; }

        public DirectoryViewModel()
        {
            Students = new ObservableCollection<StudentProfile>
            {
                new StudentProfile { Lrn = "102938475612", LastName = "Dela Cruz", FirstName = "Juan", GradeLevel = "Grade 11", Status = "Regular" },
                new StudentProfile { Lrn = "192837465521", LastName = "Rizal", FirstName = "Jose", GradeLevel = "Grade 12", Status = "Irregular" },
                new StudentProfile { Lrn = "112233445566", LastName = "Bonifacio", FirstName = "Andres", GradeLevel = "Grade 10", Status = "Regular" },
                new StudentProfile { Lrn = "998877665544", LastName = "Silang", FirstName = "Gabriela", GradeLevel = "Grade 11", Status = "Transferee" }
            };

            ViewProfileCommand = new RelayCommand<StudentProfile>(ViewProfile!);
        }

        private void ViewProfile(StudentProfile selectedStudent)
        {
            if (selectedStudent != null)
            {
                System.Console.WriteLine($"Opening full profile for: {selectedStudent.LastName}, {selectedStudent.FirstName}");
            }
        }
    }

    // A lightweight model representing Table 1: Students
    public class StudentProfile
    {
        public string Lrn { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}