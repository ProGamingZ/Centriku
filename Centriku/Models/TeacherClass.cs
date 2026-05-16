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

        public int GradingTemplateID { get; set; } // Will link to a separate templates table later if needed
    }
}