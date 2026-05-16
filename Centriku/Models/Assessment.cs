using SQLite;
using System;

namespace Centriku.Models
{
    [Table("Assessments")]
    public class Assessment
    {
        [PrimaryKey, AutoIncrement]
        public int AssessmentID { get; set; }

        [Indexed]
        public int ClassID { get; set; }

        public string? Title { get; set; }
        public string? Category { get; set; } // e.g., "Written Work", "Performance Task"
        public double MaxScore { get; set; }
        public DateTime DateGiven { get; set; }
    }
}