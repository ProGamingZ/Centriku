using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;
using System.Linq;

namespace Centriku.ViewModels
{
    // 1. SAFE DICTIONARY FOR SCORES
    public class SafeScoreDictionary : System.Collections.Generic.Dictionary<int, ScoreCellViewModel>
    {
        public new ScoreCellViewModel this[int key]
        {
            get
            {
                if (!ContainsKey(key)) Add(key, new ScoreCellViewModel(new Score(), 100, null!));
                return base[key];
            }
            set => base[key] = value;
        }
    }

    // 2. SAFE SEMANTIC DICTIONARY FOR CATEGORIES (Uses String Keys to prevent XAML binding failures)
    public class SafeCategoryDictionary : System.Collections.Generic.Dictionary<string, CategoryGradeViewModel>
    {
        public new CategoryGradeViewModel this[string key]
        {
            get
            {
                string k = key ?? "unknown";
                if (!ContainsKey(k)) Add(k, new CategoryGradeViewModel());
                return base[k];
            }
            set => base[key ?? "unknown"] = value;
        }
    }

    // 2. THE MAIN STUDENT ROW
    public partial class StudentGradeRow(Student student) : ObservableObject
    {
        public Student StudentInfo { get; } = student;
        public SafeScoreDictionary Scores { get; set; } = new();
        
        // NEW: Binds via strict text names (e.g. "majorexam") instead of fragile numbers
        public SafeCategoryDictionary CategoryGrades { get; set; } = new();

        public string FullName => $"{StudentInfo.LastName}, {StudentInfo.FirstName}";
        public string StudentID => StudentInfo.StudentID ?? "";
        [ObservableProperty] public partial bool IsSelected { get; set; } = false;
        
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