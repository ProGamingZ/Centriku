using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Centriku.Models;
using ClosedXML.Excel;
using Centriku.ViewModels; // Added to access StudentGradeRow

namespace Centriku.Services
{
   public static class ExcelExportService
   {
      public static async Task<(bool Success, string Message)> ExportToNwSSUTemplateAsync(
         TeacherClass currentClass,
         List<StudentGradeRow> gradebookRows,
         List<Assessment> classAssessments,
         List<GradingCategory> availableCategories,
         string exportDestinationFolder)
      {
         return await Task.Run(() =>
         {
            try
            {
               // 1. Get the Template Directory dynamically
               string templateDir = StorageService.GetTemplateFolderPath();
               string templatePath = Path.Combine(templateDir, "NwSSU-Class-Record.xlsx");

               if (!File.Exists(templatePath))
               {
                  return (false, $"Template missing! Please ensure 'NwSSU-Class-Record.xlsx' is inside:\n{templateDir}");
               }

               // 2. Define the output file name
               string cleanClassName = string.Join("_", (currentClass.SubjectName ?? "Class").Split(Path.GetInvalidFileNameChars()));
               string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
               string outputFilePath = Path.Combine(exportDestinationFolder, $"{cleanClassName}_ClassRecord_{timestamp}.xlsx");

               using (var workbook = new XLWorkbook(templatePath))
               {
                  // ==========================================
                  // STEP A: INJECT DATA SHEET (Master Info)
                  // ==========================================
                  var wsData = workbook.Worksheet("DATA");

                  wsData.Cell("D5").Value = currentClass.SubjectName ?? "";
                  wsData.Cell("D6").Value = currentClass.Program ?? "";
                  wsData.Cell("D7").Value = currentClass.SectionLabel ?? "";
                  wsData.Cell("R5").Value = currentClass.AcademicYear ?? "";
                  wsData.Cell("R6").Value = currentClass.Term ?? "";
                  wsData.Cell("R7").Value = currentClass.ProfessorName ?? "";

                  int currentRow = 12;
                  
                  // Sort alphabetically by last name to match the grid
                  var sortedRows = gradebookRows.OrderBy(r => r.StudentInfo.LastName).ToList();

                  foreach (var row in sortedRows)
                  {
                        if (currentRow > 111) break; 

                        var student = row.StudentInfo;
                        string mi = !string.IsNullOrWhiteSpace(student.MiddleName) ? student.MiddleName[0] + "." : "";
                        string suffix = !string.IsNullOrWhiteSpace(student.Suffix) ? student.Suffix : "";
                        string fullName = $"{student.LastName}, {student.FirstName} {mi} {suffix}".Trim().TrimEnd(',');

                        wsData.Cell(currentRow, "C").Value = student.StudentID ?? "";
                        wsData.Cell(currentRow, "D").Value = fullName;
                        wsData.Cell(currentRow, "E").Value = student.Program ?? "";
                        wsData.Cell(currentRow, "F").Value = student.GradeYearLevel ?? "";
                        currentRow++;
                  }

                  // ==========================================
                  // STEP B: INJECT MIDTERM & FINAL TERM SHEETS
                  // ==========================================
                  var wsMidterm = workbook.Worksheet("MIDTERM");
                  var wsFinal = workbook.Worksheet("FINAL TERM");

                  WriteTermData(wsMidterm, "Midterm", sortedRows, classAssessments, availableCategories);
                  WriteTermData(wsFinal, "Final", sortedRows, classAssessments, availableCategories);

                  // 3. Save the newly filled workbook
                  workbook.SaveAs(outputFilePath);
               }

               return (true, $"Success! Excel file saved to:\n{outputFilePath}");
            }
            catch (Exception ex)
            {
               return (false, $"Export Failed: {ex.Message}");
            }
         });
      }

      // HELPER METHOD: Maps Centriku Assessments to the Excel Grid Limits
      private static void WriteTermData(
         IXLWorksheet ws, 
         string termName, 
         List<StudentGradeRow> sortedRows, 
         List<Assessment> classAssessments, 
         List<GradingCategory> categories)
      {
         // 1. Get only the assessments for this specific term
         var termAssessments = classAssessments.Where(a => a.GradingPeriod == termName).ToList();

         // 2. We loop through the 3 possible sequence categories
         for (int sequence = 1; sequence <= 3; sequence++)
         {
            // Find the category assigned to this sequence slot (1=Class Standing, 2=MCO, 3=Exam)
            var currentCategory = categories.FirstOrDefault(c => c.SequenceOrder == sequence);
            if (currentCategory == null) continue;

            // Get the actual quizzes the teacher made for this category
            var targetAssessments = termAssessments.Where(a => a.Category == currentCategory.Name).ToList();

            // Set up the Excel Column Mapping Rules based on the Sequence Order
            string[] targetColumns;
            if (sequence == 1)      targetColumns = new[] { "D", "E", "F", "G", "H", "I", "J", "K", "L", "M" }; // Max 10
            else if (sequence == 2) targetColumns = new[] { "Q", "R", "S", "T", "U" };                        // Max 5
            else                    targetColumns = new[] { "Y" };                                            // Max 1

            // 3. Write Data to Excel
            for (int i = 0; i < targetAssessments.Count && i < targetColumns.Length; i++)
            {
               var assessment = targetAssessments[i];
               string colLetter = targetColumns[i];

               // Write the Max Score to Row 11
               ws.Cell(11, colLetter).Value = assessment.MaxScore;

               // Write Student Scores starting at Row 12
               int excelRow = 12;
               foreach (var studentRow in sortedRows)
               {
                  if (excelRow > 111) break; // Hard limit

                  // Check if the student has a score for this specific assessment
                  if (studentRow.Scores.TryGetValue(assessment.AssessmentID, out var scoreCell))
                  {
                     // Only write the score if it is NOT excused. If excused, leave cell blank so Excel formulas ignore it.
                     if (!scoreCell.DbModel.IsExcused)
                     {
                        ws.Cell(excelRow, colLetter).Value = scoreCell.PointsEarned;
                     }
                  }
                  excelRow++;
               }
            }
         }
      }
   
      public static async Task<(bool Success, string Message)> ExportAttendanceTemplateAsync(
         TeacherClass currentClass,
         List<AttendanceGridRowViewModel> attendanceRows,
         List<DateTime> uniqueDates,
         string exportDestinationFolder)
      {
         return await Task.Run(() =>
         {
            try
            {
               // 1. Get the Template Directory dynamically
               string templateDir = StorageService.GetTemplateFolderPath();
               string templatePath = Path.Combine(templateDir, "ATTENDANCE.xlsx");

               if (!File.Exists(templatePath))
               {
                  return (false, $"Template missing! Please ensure 'ATTENDANCE.xlsx' is inside:\n{templateDir}");
               }

               string cleanClassName = string.Join("_", (currentClass.SubjectName ?? "Class").Split(Path.GetInvalidFileNameChars()));
               string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
               string outputFilePath = Path.Combine(exportDestinationFolder, $"{cleanClassName}_Attendance_{timestamp}.xlsx");

               using (var workbook = new XLWorkbook(templatePath))
               {
                  // ==========================================
                  // STEP A: INJECT DATA SHEET
                  // ==========================================
                  var wsData = workbook.Worksheet("DATA");
                  
                  int currentRow = 4; // Starts below the header row
                  var sortedRows = attendanceRows.OrderBy(r => r.LastName).ToList();

                  int studentNumber = 1;
                  foreach (var row in sortedRows)
                  {
                     var student = row.StudentInfo;
                     string mi = !string.IsNullOrWhiteSpace(student.MiddleName) ? student.MiddleName[0] + "." : "";
                     string suffix = !string.IsNullOrWhiteSpace(student.Suffix) ? student.Suffix : "";
                     string fullName = $"{student.LastName}, {student.FirstName} {mi} {suffix}".Trim().TrimEnd(',');

                     wsData.Cell(currentRow, "A").Value = studentNumber++;
                     wsData.Cell(currentRow, "B").Value = student.StudentID ?? "";
                     wsData.Cell(currentRow, "C").Value = fullName;
                     wsData.Cell(currentRow, "D").Value = student.Program ?? "";
                     wsData.Cell(currentRow, "E").Value = student.GradeYearLevel ?? "";
                     currentRow++;
                  }

                  // ==========================================
                  // STEP B: BUILD DYNAMIC ATTENDANCE SHEET
                  // ==========================================
                  var wsAtt = workbook.Worksheet("ATTENDANCE");
                  var sortedDates = uniqueDates.OrderBy(d => d).ToList();
                  
                  int startCol = 7; // Column G

                  // Trackers for our merge logic
                  int currentYearColStart = startCol;
                  int currentMonthColStart = startCol;
                  string lastYear = "";
                  string lastMonth = "";

                  for (int i = 0; i < sortedDates.Count; i++)
                  {
                     var date = sortedDates[i];
                     int currentCol = startCol + i;

                     string thisYear = date.ToString("yyyy");
                     string thisMonth = date.ToString("MMM").ToUpper();

                     // 1. Detect if the period changed (ignoring the very first loop)
                     bool yearChanged = (thisYear != lastYear) && (i > 0);
                     bool monthChanged = (thisMonth != lastMonth || yearChanged) && (i > 0);

                     // 2. MERGE PREVIOUS PERIODS FIRST (before we update our trackers!)
                     if (monthChanged)
                     {
                         var mRange = wsAtt.Range(2, currentMonthColStart, 2, currentCol - 1);
                         if (currentCol - 1 > currentMonthColStart) mRange.Merge();
                         mRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                         mRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                     }
                         
                     if (yearChanged)
                     {
                         var yRange = wsAtt.Range(1, currentYearColStart, 1, currentCol - 1);
                         if (currentCol - 1 > currentYearColStart) yRange.Merge();
                         yRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                         yRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                     }

                     // 3. UPDATE TRACKERS & WRITE HEADERS FOR NEW PERIOD
                     if (thisYear != lastYear || i == 0) 
                     { 
                         wsAtt.Cell(1, currentCol).Value = thisYear; 
                         currentYearColStart = currentCol; 
                     }
                     if (thisMonth != lastMonth || yearChanged || i == 0) 
                     { 
                         wsAtt.Cell(2, currentCol).Value = thisMonth; 
                         currentMonthColStart = currentCol; 
                     }
                     
                     // Row 3: Day (e.g. "Mon 7")
                     wsAtt.Cell(3, currentCol).Value = date.ToString("ddd d"); 
                     wsAtt.Cell(3, currentCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                     // Write Student Statuses
                     int attRow = 4;
                     foreach (var row in sortedRows)
                     {
                        // Update the fixed stats columns (C, D, E, F)
                        wsAtt.Cell(attRow, "C").Value = row.TotalP;
                        wsAtt.Cell(attRow, "D").Value = row.TotalL;
                        wsAtt.Cell(attRow, "E").Value = row.TotalA;
                        wsAtt.Cell(attRow, "F").Value = row.TotalE;

                        string dateKey = date.ToString("yyyy-MM-dd");
                        if (row.Cells.TryGetValue(dateKey, out var cellVM))
                        {
                           wsAtt.Cell(attRow, currentCol).Value = cellVM.Status;
                           wsAtt.Cell(attRow, currentCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }
                        attRow++;
                     }

                     lastYear = thisYear;
                     lastMonth = thisMonth;
                  }

                  // Final Merge & Center Cleanup for the very last month/year in the list
                  if (sortedDates.Count > 0)
                  {
                      int finalCol = startCol + sortedDates.Count - 1;
                      
                      var mRange = wsAtt.Range(2, currentMonthColStart, 2, finalCol);
                      if (finalCol > currentMonthColStart) mRange.Merge();
                      mRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                      mRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                      var yRange = wsAtt.Range(1, currentYearColStart, 1, finalCol);
                      if (finalCol > currentYearColStart) yRange.Merge();
                      yRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                      yRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                  }

                  workbook.SaveAs(outputFilePath);
               }

               return (true, $"Success! Attendance saved to:\n{outputFilePath}");
            }
            catch (Exception ex)
            {
               return (false, $"Attendance Export Failed: {ex.Message}");
            }
         });
      }
   
   }
}