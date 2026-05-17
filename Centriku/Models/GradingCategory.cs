using SQLite;

namespace Centriku.Models
{
    [Table("GradingCategories")]
    public class GradingCategory
    {
        [PrimaryKey, AutoIncrement]
        public int CategoryID { get; set; }

        [Indexed] // Indexed because we will constantly be looking up categories by their TemplateID
        public int TemplateID { get; set; } 

        public string? Name { get; set; } // e.g., "Written Work"
        
        public double Weight { get; set; } // e.g., 30 (for 30%)
    }
}