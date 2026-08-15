using SQLite;

namespace Centriku.Models
{
    [Table("GradingCategories")]
    public class GradingCategory
    {
        [PrimaryKey, AutoIncrement]
        public int CategoryID { get; set; }

        [Indexed] 
        public int TemplateID { get; set; } 

        public string? Name { get; set; } 
        
        public double Weight { get; set; } 

        //Tracks if this is Category 1 (10 slots), 2 (5 slots), or 3 (1 slot)
        public int SequenceOrder { get; set; } 
    }
}