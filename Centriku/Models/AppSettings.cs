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

        // 4. Export Settings 
        // File & Directory
        public string DefaultExportFolderPath { get; set; } = "";
        public string FileNamingFormat { get; set; } = "[Class]_[Term]_[Date]"; 

        // Data & Privacy
        public bool ExportIncludeLRN { get; set; } = true;
        public bool ExportIncludeArchived { get; set; } = false;

        // Gradebook Formatting
        public string ExportMissingScoreRule { get; set; } = "Zero"; // Options: "Zero", "Blank", "Dash"
        public string ExportDecimalPrecision { get; set; } = "Exact"; // Options: "Exact", "Rounded"

        // Attendance Formatting
        public string ExportAttendanceDetail { get; set; } = "Detailed"; // Options: "Detailed", "SummaryOnly"

        // 5. NEW: SF9 Report Card Settings 
        // School Identity
        public string SchoolName { get; set; } = "";
        public string SchoolId { get; set; } = "";
        public string Region { get; set; } = "";
        public string Division { get; set; } = "";
        public string District { get; set; } = "";
        
        // Signatories
        public string PrincipalName { get; set; } = "";
        public string PrincipalTitle { get; set; } = "Principal I";
        public string DefaultTeacherName { get; set; } = "";
        
        // Generation & Formatting Preferences
        public string Sf9DefaultExportPath { get; set; } = "";
        public string Sf9FileNamingFormat { get; set; } = "[LastName]_[FirstName]_SF9";
        public bool Sf9AutoOpenPdf { get; set; } = true;
        public double PassingGradeThreshold { get; set; } = 75.0;
        public string BlankGradeOutput { get; set; } = "Blank"; // Options: "Blank", "Dash", "NA"

        public string LegDesc1 { get; set; } = "Outstanding";
        public string LegScale1 { get; set; } = "90-100";
        public string LegRem1 { get; set; } = "Passed";

        public string LegDesc2 { get; set; } = "Very Satisfactory";
        public string LegScale2 { get; set; } = "85-89";
        public string LegRem2 { get; set; } = "Passed";

        public string LegDesc3 { get; set; } = "Satisfactory";
        public string LegScale3 { get; set; } = "80-84";
        public string LegRem3 { get; set; } = "Passed";

        public string LegDesc4 { get; set; } = "Fairly Satisfactory";
        public string LegScale4 { get; set; } = "75-79";
        public string LegRem4 { get; set; } = "Passed";

        public string LegDesc5 { get; set; } = "Did Not Meet Expectations";
        public string LegScale5 { get; set; } = "Below 75";
        public string LegRem5 { get; set; } = "Failed";
    }
}