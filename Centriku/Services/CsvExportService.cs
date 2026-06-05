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
         string educationMode,
         bool exportAttendance,
         string customFolderPath,
         List<string> termsToExport,
         IEnumerable<StudentGradeRow> gradebookRows, 
         IEnumerable<Assessment> assessments,
         IEnumerable<AttendanceGridRowViewModel> attendanceRows,
         IEnumerable<DateTime> attendanceDates)
      {
         try
         {
            string safeTitle = string.Join("_", classTitle.Split(Path.GetInvalidFileNameChars()));
            string exportFolder = string.IsNullOrWhiteSpace(customFolderPath) ? StorageService.GetExportsFolderPath() : customFolderPath;
            string dateSuffix = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            
            int filesGenerated = 0;

            foreach (var term in termsToExport)
            {
               var termCsv = new StringBuilder();
               var headers = new List<string> { "LRN", "Last Name", "First Name" };
               
               // Determine the Headers based on what specific tab we are exporting right now
               if (term == "Final Average" || term == "Semester Average")
               {
                  if (educationMode == "Semestral") { headers.Add("Midterm Average"); headers.Add("Final Average"); }
                  else { headers.Add("Q1 Average"); headers.Add("Q2 Average"); headers.Add("Q3 Average"); headers.Add("Q4 Average"); }
                  headers.Add("Overall Final Grade");
               }
               else
               {
                  var termAssessments = assessments.Where(a => a.GradingPeriod == term).OrderBy(a => a.DateGiven).ToList();
                  foreach (var a in termAssessments) headers.Add(a.Title?.Replace(",", "") ?? "Quiz");
                  headers.Add($"{term} Average");
               }

               termCsv.AppendLine(string.Join(",", headers));

               // Build the Rows
               foreach (var row in gradebookRows)
               {
                  var rowData = new List<string> { row.StudentID, row.StudentInfo.LastName?.Replace(",", "") ?? "", row.StudentInfo.FirstName?.Replace(",", "") ?? "" };

                  if (term == "Final Average" || term == "Semester Average")
                  {
                     if (educationMode == "Semestral") { rowData.Add(row.MidtermGradeDisplay); rowData.Add(row.FinalTermGradeDisplay); }
                     else { rowData.Add(row.Q1GradeDisplay); rowData.Add(row.Q2GradeDisplay); rowData.Add(row.Q3GradeDisplay); rowData.Add(row.Q4GradeDisplay); }
                     rowData.Add(row.FinalGrade);
                  }
                  else
                  {
                     var termAssessments = assessments.Where(a => a.GradingPeriod == term).OrderBy(a => a.DateGiven).ToList();
                     foreach (var a in termAssessments)
                     {
                           if (row.Scores.TryGetValue(a.AssessmentID, out var scoreCell)) rowData.Add(scoreCell.PointsEarned.ToString("0.##"));
                           else rowData.Add("0");
                     }
                     
                     string summary = term switch { "Q1" => row.Q1GradeDisplay, "Q2" => row.Q2GradeDisplay, "Q3" => row.Q3GradeDisplay, "Q4" => row.Q4GradeDisplay, "Midterm" => row.MidtermGradeDisplay, "Final" => row.FinalTermGradeDisplay, _ => "--" };
                     rowData.Add(summary);
                  }
                  termCsv.AppendLine(string.Join(",", rowData));
               }

               // Format a clean file name (e.g., "Math101_Q1Grades_2026-06-05.csv")
               string termFileName = term.Replace(" ", ""); // Removes space for "FinalAverage"
               string termFilePath = Path.Combine(exportFolder, $"{safeTitle}_{termFileName}Grades_{dateSuffix}.csv");
               
               // Save this specific tab's file!
               await File.WriteAllTextAsync(termFilePath, termCsv.ToString());
               filesGenerated++;
            }

            if (exportAttendance)
            {
               var attendanceCsv = new StringBuilder();
               var attHeaders = new List<string> { "Last Name", "First Name", "Total P", "Total L", "Total A", "Total E" };
               foreach (var d in attendanceDates) attHeaders.Add(d.ToString("yyyy-MM-dd"));
               attendanceCsv.AppendLine(string.Join(",", attHeaders));

               foreach (var row in attendanceRows)
               {
                  var rowData = new List<string> { row.LastName.Replace(",", ""), row.FirstName.Replace(",", ""), row.TotalP.ToString(), row.TotalL.ToString(), row.TotalA.ToString(), row.TotalE.ToString() };
                  foreach (var d in attendanceDates)
                  {
                     if (row.Cells.TryGetValue(d.ToString("yyyy-MM-dd"), out var cell)) rowData.Add(cell.Status);
                     else rowData.Add("");
                  }
                  attendanceCsv.AppendLine(string.Join(",", rowData));
               }
               
               string attendanceFilePath = Path.Combine(exportFolder, $"{safeTitle}_Attendance_{dateSuffix}.csv");
               await File.WriteAllTextAsync(attendanceFilePath, attendanceCsv.ToString());
               filesGenerated++;
            }

            return (true, $"Success: Generated {filesGenerated} files to:\n{exportFolder}");
         }
         catch (Exception ex)
         {
            return (false, $"Export Failed: {ex.Message}");
         }
      }
   }
}