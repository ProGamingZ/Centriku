using SQLite;

namespace Centriku.Models
{
    [Table("AssessmentGroupMembers")]
    public class AssessmentGroupMember
    {
        [PrimaryKey, AutoIncrement]
        public int MemberID { get; set; }

        [Indexed]
        public int GroupID { get; set; }

        [Indexed]
        public int AssessmentID { get; set; }

        [Indexed]
        public string StudentID { get; set; } = string.Empty;

        public double IndividualScore { get; set; } = 0;
        public bool IsLeader { get; set; } = false;
    }
}