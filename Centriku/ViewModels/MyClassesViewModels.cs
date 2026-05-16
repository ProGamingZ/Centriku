using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels
{
    public partial class MyClassesViewModel : ViewModelBase
    {
        [ObservableProperty] 
        public partial ObservableCollection<ClassSummary> ActiveClasses { get; set; }

        // 1. Explicitly declare the command so XAML can see it
        public IRelayCommand<ClassSummary> OpenClassCommand { get; }

        public MyClassesViewModel()
        {
            ActiveClasses = new ObservableCollection<ClassSummary>
            {
                new() { SubjectName = "Advanced Mathematics", SectionLabel = "Grade 11 - Einstein", AcademicYear = "2025-2026 Q1" },
                new() { SubjectName = "Physics Fundamentals", SectionLabel = "Grade 11 - Newton", AcademicYear = "2025-2026 Q1" },
                new() { SubjectName = "Computer Science 101", SectionLabel = "Grade 12 - Turing", AcademicYear = "2025-2026 Q1" },
                new() { SubjectName = "English Literature", SectionLabel = "Grade 10 - Shakespeare", AcademicYear = "2025-2026 Q1" }
            };

            // 2. Initialize the command in the constructor
            OpenClassCommand = new RelayCommand<ClassSummary>(OpenClass!);
        }

        // 3. Removed [RelayCommand] - this is now just a standard method the command calls
        private void OpenClass(ClassSummary selectedClass)
        {
            if (selectedClass != null)
            {
                System.Console.WriteLine($"Opening Gradebook for: {selectedClass.SubjectName}");
            }
        }
    }

    public class ClassSummary
    {
        public string SubjectName { get; set; } = string.Empty;
        public string SectionLabel { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
    }
}