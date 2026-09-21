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
        public string? GradingPeriod { get; set; } = string.Empty;
        public double MaxScore { get; set; }
        public DateTime DateGiven { get; set; }
        public bool IsVisible { get; set; } = true;
        // Group Assessment Properties
        public string AssessmentType { get; set; } = "Solo"; // "Solo" or "Group/Pair"
        public double GroupWeight { get; set; } = 30.0;       // Default 30%
        public double IndividualWeight { get; set; } = 70.0;  // Default 70%
    }
}