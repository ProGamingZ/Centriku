using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centriku.Models;
using Centriku.ViewModels;

namespace Centriku.Services
{
   public static class CsvExportService
   {
      public static async Task<(bool Success, string Message)> ExportClassDataAsync(
         string classTitle, 
         IEnumerable<StudentGradeRow> gradebookRows, 
         IEnumerable<Assessment> assessments,
         IEnumerable<AttendanceGridRowViewModel> attendanceRows,
         IEnumerable<DateTime> attendanceDates)
      {
         try
         {
               // 1. Sanitize the class title for safe file naming (remove invalid characters)
               string safeTitle = string.Join("_", classTitle.Split(Path.GetInvalidFileNameChars()));
               string exportFolder = StorageService.GetExportsFolderPath();
               string dateSuffix = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
               string gradesFilePath = Path.Combine(exportFolder, $"{safeTitle}_Grades_{dateSuffix}.csv");
               string attendanceFilePath = Path.Combine(exportFolder, $"{safeTitle}_Attendance_{dateSuffix}.csv");

               // === 2. BUILD THE GRADES CSV ===
               var gradesCsv = new StringBuilder();
               
               // Grades Headers
               var gradeHeaders = new List<string> { "LRN", "Last Name", "First Name" };
               foreach (var a in assessments) gradeHeaders.Add(a.Title?.Replace(",", "") ?? "Quiz");
               gradeHeaders.Add("Final Grade");
               gradesCsv.AppendLine(string.Join(",", gradeHeaders));

               // Grades Rows
               foreach (var row in gradebookRows)
               {
                  var rowData = new List<string> 
                  { 
                     row.StudentID, 
                     row.StudentInfo.LastName?.Replace(",", "") ?? "", 
                     row.StudentInfo.FirstName?.Replace(",", "") ?? "" 
                  };

                  foreach (var a in assessments)
                  {
                     if (row.Scores.TryGetValue(a.AssessmentID, out var scoreCell))
                           rowData.Add(scoreCell.PointsEarned.ToString("0.##"));
                     else
                           rowData.Add("0");
                  }
                  rowData.Add(row.FinalGrade);
                  gradesCsv.AppendLine(string.Join(",", rowData));
               }

               // === 3. BUILD THE ATTENDANCE CSV ===
               var attendanceCsv = new StringBuilder();
               
               // Attendance Headers
               var attHeaders = new List<string> { "Last Name", "First Name", "Total P", "Total L", "Total A" };
               foreach (var d in attendanceDates) attHeaders.Add(d.ToString("yyyy-MM-dd"));
               attendanceCsv.AppendLine(string.Join(",", attHeaders));

               // Attendance Rows
               foreach (var row in attendanceRows)
               {
                  var rowData = new List<string>
                  {
                     row.LastName.Replace(",", ""),
                     row.FirstName.Replace(",", ""),
                     row.TotalP.ToString(),
                     row.TotalL.ToString(),
                     row.TotalA.ToString()
                  };

                  foreach (var d in attendanceDates)
                  {
                     string dateKey = d.ToString("yyyy-MM-dd");
                     if (row.Cells.TryGetValue(dateKey, out var cell))
                           rowData.Add(cell.Status);
                     else
                           rowData.Add("");
                  }
                  attendanceCsv.AppendLine(string.Join(",", rowData));
               }

               // 4. Save both files to disk!
               await File.WriteAllTextAsync(gradesFilePath, gradesCsv.ToString());
               await File.WriteAllTextAsync(attendanceFilePath, attendanceCsv.ToString());

               return (true, $"Exported successfully to:\n{exportFolder}");
         }
         catch (Exception ex)
         {
               return (false, $"Export Failed: {ex.Message}");
         }
      }
   }
}