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

        public string? AttendanceCalculationMode { get; set; } = "None"; 
        public int MaxAbsencesAllowed { get; set; } = 3; 
        public double AttendanceWeight { get; set; } = 10.0; 
        public double LateValue { get; set; } = 0.5;

        // NOTE: EducationMode has been completely removed!
    }
}