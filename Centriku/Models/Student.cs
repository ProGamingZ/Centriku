using SQLite;
using System;

namespace Centriku.Models
{
    [Table("Students")]
    public class Student
    {
        [PrimaryKey]
        public string? StudentID { get; set; } // e.g., LRN, handled as string to allow dashes

        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? Suffix { get; set; }
        public string? Gender { get; set; }
    }
}