using SQLite;
using System;

namespace Centriku.Models
{
    [Table("AttendanceRecords")]
    public class AttendanceRecord
    {
        [PrimaryKey, AutoIncrement]
        public int RecordID { get; set; }

        [Indexed] // Indexed because we will constantly filter by Class
        public int ClassID { get; set; }

        [Indexed] // Indexed because we will constantly filter by Student
        public string? StudentID { get; set; }
        public DateTime Date { get; set; }

        // We will use standard strings for this: "Present", "Late", "Absent", "Excused"
        public string? Status { get; set; } 
    }
}