using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;

namespace Centriku.ViewModels
{
    public partial class StudentGradeRow(Student student) : ObservableObject
    {
        public Student StudentInfo { get; } = student;
        public System.Collections.Generic.Dictionary<int, ScoreCellViewModel> Scores { get; set; } = [];
        public string FullName => $"{StudentInfo.LastName}, {StudentInfo.FirstName}";
        public string StudentID => StudentInfo.StudentID ?? "";
        
        [ObservableProperty] public partial string MidtermGradeDisplay { get; set; } = "---";
        [ObservableProperty] public partial string FinalTermGradeDisplay { get; set; } = "---";
        [ObservableProperty] public partial double MidtermGradeNumeric { get; set; } = 0;
        [ObservableProperty] public partial double FinalTermGradeNumeric { get; set; } = 0;
        
        [ObservableProperty] public partial string FinalGrade { get; set; } = "---";
        [ObservableProperty] public partial double FinalGradeNumeric { get; set; } = 0;

        //These hold the math computations for the UI Tooltips!
        [ObservableProperty] public partial string MidtermComputationTooltip { get; set; } = string.Empty;
        [ObservableProperty] public partial string FinalComputationTooltip { get; set; } = string.Empty;
        [ObservableProperty] public partial string FinalGradeTooltip { get; set; } = string.Empty;
        
    }
}