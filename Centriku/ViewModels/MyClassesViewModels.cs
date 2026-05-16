using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels
{
    public partial class MyClassesViewModel : ViewModelBase
    {
        [ObservableProperty] 
        public partial ObservableCollection<ClassSummary> ActiveClasses { get; set; }
        public IRelayCommand<ClassSummary> OpenClassCommand { get; }
        private readonly Action<ViewModelBase> _navigateAction;
        public MyClassesViewModel(Action<ViewModelBase> navigateAction)
        {
            _navigateAction = navigateAction;

            ActiveClasses = new ObservableCollection<ClassSummary>
            {
                new() { SubjectName = "Advanced Mathematics", SectionLabel = "Grade 11 - Einstein", AcademicYear = "2025-2026 Q1" },
                new() { SubjectName = "Physics Fundamentals", SectionLabel = "Grade 11 - Newton", AcademicYear = "2025-2026 Q1" },
                new() { SubjectName = "Computer Science 101", SectionLabel = "Grade 12 - Turing", AcademicYear = "2025-2026 Q1" },
                new() { SubjectName = "English Literature", SectionLabel = "Grade 10 - Shakespeare", AcademicYear = "2025-2026 Q1" }
            };
            OpenClassCommand = new RelayCommand<ClassSummary>(OpenClass!);
        }

        private void OpenClass(ClassSummary selectedClass)
        {
            if (selectedClass != null)
            {
                var gradebookVM = new GradebookViewModel 
                { 
                    ClassTitle = selectedClass.SubjectName 
                };
                
                _navigateAction(gradebookVM);
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