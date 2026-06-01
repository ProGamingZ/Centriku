using SQLite;

namespace Centriku.Models
{
    [Table("MasterQuarterlyGrades")]
    public class MasterQuarterlyGrade
    {
        [PrimaryKey, AutoIncrement]
        public int GradeID { get; set; }

        [Indexed]
        public string? StudentID { get; set; }
        
        public string? AcademicYear { get; set; } // e.g., "2024-2025"
        public string? SubjectName { get; set; }  // e.g., "Earth Science"
        
        // Using nullable doubles (?) so you know if a grade hasn't been encoded yet
        public double? Quarter1Grade { get; set; }
        public double? Quarter2Grade { get; set; }
        public double? Quarter3Grade { get; set; }
        public double? Quarter4Grade { get; set; }
        
        public double? FinalRating { get; set; }
        public string? Remarks { get; set; } // e.g., "Passed", "Failed"
    }
}