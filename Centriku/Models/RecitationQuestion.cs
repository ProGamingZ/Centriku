using SQLite;

namespace Centriku.Models
{
    [Table("RecitationQuestions")]
    public class RecitationQuestion
    {
        [PrimaryKey, AutoIncrement]
        public int QuestionID { get; set; }

        [Indexed] // Indexed because we will constantly filter questions by the current active class
        public int ClassID { get; set; }

        public string QuestionText { get; set; } = string.Empty;
        public string AnswerText { get; set; } = string.Empty;
        
        public bool IsIncluded { get; set; } = true;
    }
}