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
         bool exportAttendance, // Removed educationMode parameter
         string customFolderPath,
         List<string> termsToExport,
         IEnumerable<StudentGradeRow> gradebookRows, 
         IEnumerable<Assessment> assessments,
         IEnumerable<AttendanceGridRowViewModel> attendanceRows,
         IEnumerable<DateTime> attendanceDates,
         AppSettings settings) 
      {
         try
         {
            string safeTitle = string.Join("_", classTitle.Split(Path.GetInvalidFileNameChars()));
            
            // 1. FOLDER RESOLUTION
            string defaultGlobalFolder = string.IsNullOrWhiteSpace(settings.DefaultExportFolderPath) ? StorageService.GetExportsFolderPath() : settings.DefaultExportFolderPath;
            string exportFolder = string.IsNullOrWhiteSpace(customFolderPath) ? defaultGlobalFolder : customFolderPath;
            
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            
            // 2. FILE NAMING RULE HELPER
            string GenerateFileName(string termName)
            {
               string format = settings.FileNamingFormat ?? "[Class]_[Term]_[Date]";
               string cleanTerm = termName.Replace(" ", "");
               return format.Replace("[Class]", safeTitle).Replace("[Term]", cleanTerm).Replace("[Date]", dateStr) + ".csv";
            }

            int filesGenerated = 0;

            // === 1. BUILD INDIVIDUAL GRADE FILES ===
            foreach (var term in termsToExport)
            {
               var termCsv = new StringBuilder();
               var headers = new List<string>();
               
               // PRIVACY RULE: Include Student ID?
               if (settings.ExportIncludeStudentId) headers.Add("Student ID");
               headers.Add("Last Name");
               headers.Add("First Name");
               
               if (term == "Semester Average")
               {
                  headers.Add("Midterm Grade"); 
                  headers.Add("Final Grade"); 
                  headers.Add(term); // Outputs the literal words "Semester Average"
               }
               else
               {
                  var termAssessments = assessments.Where(a => a.GradingPeriod == term).OrderBy(a => a.DateGiven).ToList();
                  foreach (var a in termAssessments) headers.Add(a.Title?.Replace(",", "") ?? "Assessment");
                  
                  headers.Add($"{term} Grade");
               }

               termCsv.AppendLine(string.Join(",", headers));

               foreach (var row in gradebookRows)
               {
                  var rowData = new List<string>();
                  if (settings.ExportIncludeStudentId) rowData.Add(row.StudentID ?? "");
                  rowData.Add(row.StudentInfo.LastName?.Replace(",", "") ?? "");
                  rowData.Add(row.StudentInfo.FirstName?.Replace(",", "") ?? "");

                  if (term == "Semester Average")
                  {
                     rowData.Add(row.MidtermGradeDisplay); 
                     rowData.Add(row.FinalTermGradeDisplay); 
                     rowData.Add(row.FinalGrade);
                  }
                  else
                  {
                     var termAssessments = assessments.Where(a => a.GradingPeriod == term).OrderBy(a => a.DateGiven).ToList();
                     foreach (var a in termAssessments)
                     {
                        if (row.Scores.TryGetValue(a.AssessmentID, out var scoreCell)) 
                        {
                           // FORMATTING RULE: Decimal Precision
                           if (settings.ExportDecimalPrecision == "Rounded")
                              rowData.Add(Math.Round(scoreCell.PointsEarned, 0).ToString());
                           else
                              rowData.Add(scoreCell.PointsEarned.ToString("0.##"));
                        }
                        else 
                        {
                           // FORMATTING RULE: Missing Scores
                           string blankOutput = settings.ExportMissingScoreRule switch {
                              "Blank" => "",
                              "Dash" => "--",
                              _ => "0"
                           };
                           rowData.Add(blankOutput);
                        }
                     }
                     // Map to Semestral Summaries
                     string summary = term switch { "Midterm" => row.MidtermGradeDisplay, "Final" => row.FinalTermGradeDisplay, _ => "--" };
                     rowData.Add(summary);
                  }
                  termCsv.AppendLine(string.Join(",", rowData));
               }

               string termFilePath = Path.Combine(exportFolder, GenerateFileName(term));
               await File.WriteAllTextAsync(termFilePath, termCsv.ToString());
               filesGenerated++;
            }

            // === 2. BUILD THE ATTENDANCE CSV ===
            if (exportAttendance)
            {
               var attendanceCsv = new StringBuilder();
               var attHeaders = new List<string> { "Last Name", "First Name", "Total P", "Total L", "Total A", "Total E" };
               
               // FORMATTING RULE: Detailed vs Summary Attendance
               if (settings.ExportAttendanceDetail == "Detailed")
               {
                  foreach (var d in attendanceDates) attHeaders.Add(d.ToString("yyyy-MM-dd"));
               }
               
               attendanceCsv.AppendLine(string.Join(",", attHeaders));

               foreach (var row in attendanceRows)
               {
                  var rowData = new List<string> { row.LastName.Replace(",", ""), row.FirstName.Replace(",", ""), row.TotalP.ToString(), row.TotalL.ToString(), row.TotalA.ToString(), row.TotalE.ToString() };
                  
                  if (settings.ExportAttendanceDetail == "Detailed")
                  {
                     foreach (var d in attendanceDates)
                     {
                        if (row.Cells.TryGetValue(d.ToString("yyyy-MM-dd"), out var cell)) rowData.Add(cell.Status);
                        else rowData.Add("");
                     }
                  }
                  attendanceCsv.AppendLine(string.Join(",", rowData));
               }
               
               string attendanceFilePath = Path.Combine(exportFolder, GenerateFileName("Attendance"));
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