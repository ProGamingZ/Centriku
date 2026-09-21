using SQLite;

namespace Centriku.Models
{
    [Table("AssessmentGroups")]
    public class AssessmentGroup
    {
        [PrimaryKey, AutoIncrement]
        public int GroupID { get; set; }

        [Indexed]
        public int AssessmentID { get; set; }

        [Indexed]
        public int ClassID { get; set; }

        public string GroupName { get; set; } = string.Empty;
        public double GroupScore { get; set; } = 0;
    }
}