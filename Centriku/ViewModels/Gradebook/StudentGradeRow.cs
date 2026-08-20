using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;

namespace Centriku.ViewModels
{
    // 1. SAFE DICTIONARIES TO PREVENT AVALONIA BINDING CRASHES
    public class SafeScoreDictionary : System.Collections.Generic.Dictionary<int, ScoreCellViewModel>
    {
        public new ScoreCellViewModel this[int key]
        {
            get
            {
                // If the UI asks for a score cell that hasn't been loaded yet, return a safe dummy cell!
                if (!ContainsKey(key))
                    Add(key, new ScoreCellViewModel(new Score(), 100, null!));
                return base[key];
            }
            set => base[key] = value;
        }
    }

    public class SafeCategoryDictionary : System.Collections.Generic.Dictionary<int, CategoryGradeViewModel> // <-- Changed string to int
    {
        public new CategoryGradeViewModel this[int key] // <-- Changed string to int
        {
            get
            {
                if (!ContainsKey(key))
                    Add(key, new CategoryGradeViewModel());
                return base[key];
            }
            set => base[key] = value;
        }
    }

    // 2. THE MAIN STUDENT ROW
    public partial class StudentGradeRow(Student student) : ObservableObject
    {
        public Student StudentInfo { get; } = student;
        public SafeScoreDictionary Scores { get; set; } = new();
        public SafeCategoryDictionary CategoryGrades { get; set; } = new();

        public string FullName => $"{StudentInfo.LastName}, {StudentInfo.FirstName}";
        public string StudentID => StudentInfo.StudentID ?? "";
        
        [ObservableProperty] public partial string MidtermGradeDisplay { get; set; } = "---";
        [ObservableProperty] public partial string FinalTermGradeDisplay { get; set; } = "---";
        [ObservableProperty] public partial double MidtermGradeNumeric { get; set; } = 0;
        [ObservableProperty] public partial double FinalTermGradeNumeric { get; set; } = 0;
        
        [ObservableProperty] public partial string FinalGrade { get; set; } = "---";
        [ObservableProperty] public partial double FinalGradeNumeric { get; set; } = 0;

        [ObservableProperty] public partial string MidtermComputationTooltip { get; set; } = string.Empty;
        [ObservableProperty] public partial string FinalComputationTooltip { get; set; } = string.Empty;
        [ObservableProperty] public partial string FinalGradeTooltip { get; set; } = string.Empty;
    }

    // 3. THE TS / WS DATA MODEL
    public partial class CategoryGradeViewModel : ObservableObject
    {
        [ObservableProperty] public partial string TsDisplay { get; set; } = "--";
        [ObservableProperty] public partial string WsDisplay { get; set; } = "--";
        [ObservableProperty] public partial string TsTooltip { get; set; } = string.Empty;
        [ObservableProperty] public partial string WsTooltip { get; set; } = string.Empty;
    }
}