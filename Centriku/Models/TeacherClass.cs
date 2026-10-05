using SQLite;

namespace Centriku.Models
{
    [Table("Classes")]
    public class TeacherClass
    {
        [PrimaryKey, AutoIncrement]
        public int ClassID { get; set; }

        public string? AcademicYear { get; set; } 
        public string? Term { get; set; }         
        public string? SubjectName { get; set; }
        public string? SectionLabel { get; set; }

        public string? Program { get; set; }
        public string? ProfessorName { get; set; }

        public int GradingTemplateID { get; set; } 

        public bool ShowStudentId { get; set; } = true;
        public bool ShowFirstName { get; set; } = true;
        public bool ShowLastName { get; set; } = true;
        public bool ShowFinalGrade { get; set; } = true;
        public bool ShowTotalP { get; set; } = true;
        public bool ShowTotalL { get; set; } = true;
        public bool ShowTotalA { get; set; } = true;
        public bool ShowTotalE { get; set; } = true; 
        public bool IsActive { get; set; } = true;
        // --- NEW: FDA (Failure Due to Absences) Configuration ---
        public bool IsFdaEnabled { get; set; } = false;
        
        // RuleType: "Equivalent" (Lates convert to absences) or "Independent" (Separate limits)
        public string FdaRuleType { get; set; } = "Equivalent"; 
        
        public double FdaMaxAbsenceHours { get; set; } = 10.0;
        
        // E.g., 0.33 means 1 late hour equals 0.33 absent hours (3 lates = 1 absence)
        public double FdaLatePenaltyRatio { get; set; } = 0.33; 
        
        // Used ONLY if RuleType is "Independent"
        public double FdaMaxLateHours { get; set; } = 6.0; 
        public string FdaDropGrade { get; set; } = "5.0"; // or "5.0", "DRP"
    }
}