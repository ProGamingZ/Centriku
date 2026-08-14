using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Centriku.Models;
using ClosedXML.Excel;

namespace Centriku.Services
{
   public static class ExcelExportService
   {
      public static async Task<(bool Success, string Message)> ExportToNwSSUTemplateAsync(
      TeacherClass currentClass,List<Student> students,string exportDestinationFolder)
      {
         return await Task.Run(() =>
         {
            try
            {
               // 1. Get the Template Directory dynamically from our StorageService
               string templateDir = StorageService.GetTemplateFolderPath();
               string templatePath = Path.Combine(templateDir, "NwSSU-Class-Record.xlsx");

               // 2. Check if the template exists
               if (!File.Exists(templatePath))
               {
                  return (false, $"Template missing! Please ensure 'NwSSU-Class-Record.xlsx' is inside:\n{templateDir}");
               }

               // 3. Define the output file name
               string cleanClassName = string.Join("_", (currentClass.SubjectName ?? "Class").Split(Path.GetInvalidFileNameChars()));
               string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
               string outputFilePath = Path.Combine(exportDestinationFolder, $"{cleanClassName}_ClassRecord_{timestamp}.xlsx");

               // 4. Open the template using ClosedXML
               using (var workbook = new XLWorkbook(templatePath))
               {
                  // Target the "DATA" sheet
                  var ws = workbook.Worksheet("DATA");

                  // 5. Inject Header Data
                  ws.Cell("D5").Value = currentClass.SubjectName ?? "";
                  ws.Cell("D6").Value = currentClass.Program ?? "";
                  ws.Cell("D7").Value = currentClass.SectionLabel ?? "";
                  
                  ws.Cell("R5").Value = currentClass.AcademicYear ?? "";
                  ws.Cell("R6").Value = currentClass.Term ?? "";
                  ws.Cell("R7").Value = currentClass.ProfessorName ?? "";

                  // 6. Inject Student Roster (Starts at Row 12, Ends at Row 111)
                  int currentRow = 12;
                  
                  // Sort students by Last Name for standard class record format
                  var sortedStudents = students.OrderBy(s => s.LastName).ToList();

                  foreach (var student in sortedStudents)
                  {
                        if (currentRow > 111) break; // Hard limit set by the template

                        // Construct Name: Lastname, Firstname MI. Suffix
                        string mi = !string.IsNullOrWhiteSpace(student.MiddleName) ? student.MiddleName[0] + "." : "";
                        string suffix = !string.IsNullOrWhiteSpace(student.Suffix) ? student.Suffix : "";
                        
                        // Clean up extra spaces
                        string fullName = $"{student.LastName}, {student.FirstName} {mi} {suffix}".Trim();
                        if (fullName.EndsWith(",")) fullName = fullName.TrimEnd(','); // Handle edge case

                        // Populate Columns C through F
                        ws.Cell(currentRow, "C").Value = student.StudentID ?? "";
                        ws.Cell(currentRow, "D").Value = fullName;
                        ws.Cell(currentRow, "E").Value = student.Program ?? "";
                        ws.Cell(currentRow, "F").Value = student.GradeYearLevel ?? "";

                        currentRow++;
                  }

                  // 7. Save the newly filled workbook
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
   }
}