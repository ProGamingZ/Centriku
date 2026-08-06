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
        public int StudentIdColumnIndex { get; set; } = 0; 
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

        // 4. Export Settings 
        // File & Directory
        public string DefaultExportFolderPath { get; set; } = "";
        public string FileNamingFormat { get; set; } = "[Class]_[Term]_[Date]"; 

        // Data & Privacy
        public bool ExportIncludeStudentId { get; set; } = true;
        public bool ExportIncludeArchived { get; set; } = false;

        // Gradebook Formatting
        public string ExportMissingScoreRule { get; set; } = "Zero"; // Options: "Zero", "Blank", "Dash"
        public string ExportDecimalPrecision { get; set; } = "Exact"; // Options: "Exact", "Rounded"

        // Attendance Formatting
        public string ExportAttendanceDetail { get; set; } = "Detailed"; // Options: "Detailed", "SummaryOnly"
        
        public int ProgramColumnIndex { get; set; } = -1;
        public int SectionNameColumnIndex { get; set; } = -1;
        public string DefaultProgram { get; set; } = string.Empty;
        public string DefaultSectionName { get; set; } = string.Empty;
    }
}