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
        public bool ShowTotalP { get; set; } = true;
        public bool ShowTotalL { get; set; } = true;
        public bool ShowTotalA { get; set; } = true;
    }
}