using SQLite;

namespace Centriku.Models
{
    [Table("AppSettings")]
    public class AppSettings
    {
        // We force the ID to always be 1, because we only ever need ONE row of settings for the whole app!
        [PrimaryKey]
        public int Id { get; set; } = 1; 

        // 1. Column Mappings (0 = Col A, 1 = Col B, etc. | -1 = Ignore this column)
        public int LrnColumnIndex { get; set; } = 0;
        public int LastNameColumnIndex { get; set; } = 1;
        public int FirstNameColumnIndex { get; set; } = 2;
        public int MiddleNameColumnIndex { get; set; } = 3;
        public int SuffixColumnIndex { get; set; } = -1;
        
        // Let's default these optional ones to -1 (Ignore) so the app doesn't crash if they are missing
        public int GenderColumnIndex { get; set; } = -1;
        public int GradeLevelColumnIndex { get; set; } = -1;
        public int SectionColumnIndex { get; set; } = -1;
        public int EnrollmentStatusColumnIndex { get; set; } = -1;

        // 2. Import Rules
        public bool SkipFirstRow { get; set; } = true;
        public string DuplicateHandlingRule { get; set; } = "Update";
        public bool AutoCapitalizeNames { get; set; } = true;
        public bool SkipIncompleteRows { get; set; } = true;

        // 3. Fallback Defaults (What to inject if the column is blank or ignored)
        public string DefaultGender { get; set; } = "Unspecified";
        public string DefaultGradeLevel { get; set; } = "";
        public string DefaultSection { get; set; } = "";
        public string DefaultEnrollmentStatus { get; set; } = "Regular";
    }
}