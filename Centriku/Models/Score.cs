using SQLite;

namespace Centriku.Models
{
    [Table("Scores")]
    public class Score
    {
        [PrimaryKey, AutoIncrement]
        public int ScoreID { get; set; }

        [Indexed]
        public int AssessmentID { get; set; }

        [Indexed]
        public string? StudentID { get; set; }

        public double PointsEarned { get; set; }
        public bool IsExcused { get; set; } // True if blank/excused, ignores PointsEarned
    }
}