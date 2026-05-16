using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
    public partial class GradebookViewModel : ViewModelBase
    {
        // This is the title at the top of the screen (e.g., "Advanced Mathematics")
        [ObservableProperty]
        public partial string ClassTitle { get; set; } = "Loading Class...";

        // This is the actual list of students that will feed into the DataGrid
        [ObservableProperty]
        public partial ObservableCollection<StudentGradeRow> Students { get; set; }

        public GradebookViewModel()
        {
            // We will inject dummy data for now so we have something to look at when we build the UI
            Students =
            [
                new StudentGradeRow { Lrn = "102938475612", FullName = "Dela Cruz, Juan", Quiz1 = 15, Quiz2 = 18, FinalExam = 85 },
                new StudentGradeRow { Lrn = "192837465521", FullName = "Rizal, Jose", Quiz1 = 20, Quiz2 = 19, FinalExam = 92 },
                new StudentGradeRow { Lrn = "112233445566", FullName = "Bonifacio, Andres", Quiz1 = 12, Quiz2 = 14, FinalExam = 78 },
                new StudentGradeRow { Lrn = "998877665544", FullName = "Silang, Gabriela", Quiz1 = 20, Quiz2 = 20, FinalExam = 98 }
            ];
        }
    }

    // --- THIS REPRESENTS A SINGLE ROW IN THE DATAGRID ---
    public partial class StudentGradeRow : ObservableObject
    {
        [ObservableProperty]
        public partial string Lrn { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string FullName { get; set; } = string.Empty;

        // Note: Using 'double?' allows a score to be blank/null if they missed the quiz
        [ObservableProperty]
        public partial double? Quiz1 { get; set; }

        [ObservableProperty]
        public partial double? Quiz2 { get; set; }

        [ObservableProperty]
        public partial double? FinalExam { get; set; }
        
        // This calculates the total automatically whenever a score changes!
        public double ComputedGrade 
        {
            get 
            {
                // A very basic temporary formula: (Quiz1 + Quiz2 + Exam) 
                double q1 = Quiz1 ?? 0;
                double q2 = Quiz2 ?? 0;
                double exam = FinalExam ?? 0;
                return q1 + q2 + exam;
            }
        }
    }
}