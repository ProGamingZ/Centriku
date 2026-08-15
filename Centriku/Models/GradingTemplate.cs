using SQLite;

namespace Centriku.Models
{
    [Table("GradingTemplates")]
    public class GradingTemplate
    {
        [PrimaryKey, AutoIncrement]
        public int TemplateID { get; set; }

        public string? TemplateName { get; set; } // e.g., "DepEd SHS Core Subject"
        
        // Note: Using double to keep your data types consistent with Score.cs and Assessment.cs
        public double PassingGrade { get; set; } // e.g., 75
        public double NrfgBaseValue { get; set; } = 50.0;
    }
}