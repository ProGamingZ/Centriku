using SQLite;

namespace Centriku.Models
{
    [Table("ClassRoster")]
    public class ClassRoster
    {
        [PrimaryKey, AutoIncrement]
        public int RosterID { get; set; }

        [Indexed] // Indexed for faster lookups when querying a specific class
        public int ClassID { get; set; }

        [Indexed] 
        public string? StudentID { get; set; }
        
        public bool HasRecited { get; set; } = false;
    }
}