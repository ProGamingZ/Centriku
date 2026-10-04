using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;
using Centriku.Services;
using System.Linq;

namespace Centriku.ViewModels
{
    // 1. SAFE DICTIONARY FOR SCORES
    public class SafeScoreDictionary : System.Collections.Generic.Dictionary<int, ScoreCellViewModel>
    {
        private readonly IDatabaseService _dbService;
        
        public SafeScoreDictionary(IDatabaseService dbService)
        {
            _dbService = dbService;
        }

        public new ScoreCellViewModel this[int key]
        {
            get
            {
                if (!ContainsKey(key)) Add(key, new ScoreCellViewModel(_dbService, new Score(), 100, null!));
                return base[key];
            }
            set => base[key] = value;
        }
    }

    // 2. SAFE SEMANTIC DICTIONARY FOR CATEGORIES
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

    // 3. THE MAIN STUDENT ROW (Primary Constructor Syntax Removed)
    public partial class StudentGradeRow : ObservableObject
    {
        public Student StudentInfo { get; }
        public SafeScoreDictionary Scores { get; set; } 
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

        // Traditional Constructor properly initializes non-nullable properties
        public StudentGradeRow(IDatabaseService dbService, Student student)
        {
            StudentInfo = student;
            Scores = new SafeScoreDictionary(dbService);
        }
    }

    // 4. THE TS / WS DATA MODEL
    public partial class CategoryGradeViewModel : ObservableObject
    {
        [ObservableProperty] public partial string TsDisplay { get; set; } = "--";
        [ObservableProperty] public partial string WsDisplay { get; set; } = "--";
        [ObservableProperty] public partial string TsTooltip { get; set; } = string.Empty;
        [ObservableProperty] public partial string WsTooltip { get; set; } = string.Empty;
    }
}