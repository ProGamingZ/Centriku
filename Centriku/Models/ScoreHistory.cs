using SQLite;
using System;

namespace Centriku.Models
{
    [Table("ScoreHistory")]
    public class ScoreHistory
    {
        [PrimaryKey, AutoIncrement]
        public int LogID { get; set; }

        public string? ActionType { get; set; } // "Modified" or "Deleted"
        
        public int ScoreID { get; set; }
        public int AssessmentID { get; set; }
        public string? StudentID { get; set; }
        
        public double OldValue { get; set; }
        public DateTime Timestamp { get; set; }
    }
}