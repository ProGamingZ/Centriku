using SQLite;

namespace Centriku.Models
{
    [Table("Classes")]
    public class TeacherClass
    {
        [PrimaryKey, AutoIncrement]
        public int ClassID { get; set; }

        public string? AcademicYear { get; set; } // e.g., "2024-2025"
        public string? Term { get; set; }         // e.g., "Q1" or "1st Semester"
        public string? SubjectName { get; set; }
        public string? SectionLabel { get; set; }

        public int GradingTemplateID { get; set; } 

        public bool ShowLRN { get; set; } = true;
        public bool ShowFirstName { get; set; } = true;
        public bool ShowLastName { get; set; } = true;
        public bool ShowFinalGrade { get; set; } = true;
        public bool ShowTotalP { get; set; } = true;
        public bool ShowTotalL { get; set; } = true;
        public bool ShowTotalA { get; set; } = true;

        // Mode: "None", "Threshold", "Weighted", or "Bonus"
        public string? AttendanceCalculationMode { get; set; } = "None"; 
        
        // If Mode is "Threshold": How many absences trigger an automatic Fail?
        public int MaxAbsencesAllowed { get; set; } = 3; 

        // If Mode is "Weighted" or "Bonus": How much % is attendance worth?
        public double AttendanceWeight { get; set; } = 10.0; 
        
        // How much penalty does a "Late" (L) carry? 
        // e.g., 0.5 means 2 Lates = 1 Absent. 0.0 means Lates are ignored mathematically.
        public double LateValue { get; set; } = 0.5;
    }
}