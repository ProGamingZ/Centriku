using SQLite;

namespace Centriku.Models
{
    [Table("GradeBoundaries")]
    public class GradeBoundary
    {
        [PrimaryKey, AutoIncrement]
        public int BoundaryID { get; set; }

        [Indexed] 
        public int TemplateID { get; set; }

        public double MinScore { get; set; }

        public double MaxScore { get; set; }

        // The text that appears on the report card (e.g., "A", "1.0", "Pass", "First-Class")
        public string? Label { get; set; }

        // Optional: For US schools that map letters to a 4.0 GPA scale
        public double GpaValue { get; set; } 
    }
}