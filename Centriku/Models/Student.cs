using SQLite;
using System;

namespace Centriku.Models
{
    [Table("Students")]
    public class Student
    {
        [PrimaryKey]
        public string? StudentID { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Suffix { get; set; }
        public string? Gender { get; set; }

        public string? GradeYearLevel { get; set; }
        public string? Program { get; set; }       
        public string? SectionName { get; set; }
        public string? EnrollmentStatus { get; set; }

        public bool IsArchived { get; set; } = false;
    }
}