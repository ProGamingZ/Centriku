using System;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Centriku.ViewModels;

namespace Centriku.Services
{
   public class Sf9Generator
   {
      public static void GenerateReportCard(DirectoryViewModel vm, string destinationPath)
      {
         QuestPDF.Settings.License = LicenseType.Community;
         var student = vm.SelectedProfile;
         if (student == null) return;

         var document = Document.Create(container =>
         {
               container.Page(page =>
               {
                  page.Size(PageSizes.A4);
                  page.Margin(2, Unit.Centimetre);
                  page.PageColor(Colors.White);
                  page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                  page.Header().Element(ComposeHeader);
                  page.Content().Element(contentContainer => ComposeContent(contentContainer, vm));
                  
                  page.Footer().AlignCenter().Text(x =>
                  {
                     x.Span("Page ");
                     x.CurrentPageNumber();
                     x.Span(" of ");
                     x.TotalPages();
                  });
               });
         });

         document.GeneratePdf(destinationPath);
      }

      private static void ComposeHeader(IContainer container)
      {
         container.Row(row =>
         {
               row.RelativeItem().Column(column =>
               {
                  column.Item().Text("Republic of the Philippines").FontSize(10).AlignCenter();
                  column.Item().Text("Department of Education").FontSize(14).Bold().AlignCenter();
                  column.Item().Text("Learner Progress Report Card (SF9)").FontSize(10).AlignCenter();
               });
         });
      }

      private static void ComposeContent(IContainer container, DirectoryViewModel vm)
      {
         var student = vm.SelectedProfile!;

         container.PaddingVertical(1, Unit.Centimetre).Column(column =>
         {
               column.Spacing(10);
               
               // ==========================================
               // PAGE 1: ACADEMICS
               // ==========================================
               
               // 1. Demographics Grid
               column.Item().Table(table =>
               {
                  table.ColumnsDefinition(columns =>
                  {
                     columns.RelativeColumn(3);
                     columns.RelativeColumn(1);
                  });

                  table.Cell().Text($"Name: {student.LastName}, {student.FirstName} {student.MiddleName}").Bold();
                  table.Cell().Text($"LRN: {student.StudentID}");
                  table.Cell().Text($"Grade & Section: {student.GradeYearLevel} - {student.SectionBlock}");
                  table.Cell().Text($"Sex: {student.Gender}");
               });
               
               column.Item().PaddingTop(10).Text("REPORT ON LEARNING PROGRESS AND ACHIEVEMENT").FontSize(12).Bold().AlignCenter();

               // 2. The Master Academic Grid
               column.Item().Table(table =>
               {
                  table.ColumnsDefinition(columns =>
                  {
                     columns.RelativeColumn(3);    
                     columns.RelativeColumn(1);    
                     columns.RelativeColumn(1);    
                     columns.RelativeColumn(1);    
                     columns.RelativeColumn(1);    
                     columns.RelativeColumn(1.5f); 
                     columns.RelativeColumn(1.5f); 
                  });

                  static IContainer HeaderStyle(IContainer c) => c.Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().AlignMiddle();

                  table.Header(header =>
                  {
                     header.Cell().Element(HeaderStyle).Text("Learning Areas").SemiBold();
                     header.Cell().Element(HeaderStyle).Text("1").SemiBold();
                     header.Cell().Element(HeaderStyle).Text("2").SemiBold();
                     header.Cell().Element(HeaderStyle).Text("3").SemiBold();
                     header.Cell().Element(HeaderStyle).Text("4").SemiBold();
                     header.Cell().Element(HeaderStyle).Text("Final Grade").SemiBold();
                     header.Cell().Element(HeaderStyle).Text("Remarks").SemiBold();
                  });

                  static IContainer CellStyle(IContainer c) => c.Border(1).BorderColor(Colors.Black).Padding(4);

                  foreach (var row in vm.MasterGrades)
                  {
                     table.Cell().Element(CellStyle).Text(row.SubjectName);
                     table.Cell().Element(CellStyle).AlignCenter().Text(row.Q1Text);
                     table.Cell().Element(CellStyle).AlignCenter().Text(row.Q2Text);
                     table.Cell().Element(CellStyle).AlignCenter().Text(row.Q3Text);
                     table.Cell().Element(CellStyle).AlignCenter().Text(row.Q4Text);
                     table.Cell().Element(CellStyle).AlignCenter().Text(row.FinalGrade).SemiBold();
                     table.Cell().Element(CellStyle).AlignCenter().Text(row.Remarks);
                  }

                  string generalRemarks = "--";
                  if (vm.FinalGeneralAverage != "--" && double.TryParse(vm.FinalGeneralAverage, out double fga))
                  {
                     generalRemarks = fga >= 75 ? "Passed" : "Failed";
                  }

                  table.Cell().Element(CellStyle).AlignRight().Text("General Average").SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(vm.Q1Average).SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(vm.Q2Average).SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(vm.Q3Average).SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(vm.Q4Average).SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(vm.FinalGeneralAverage).SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(generalRemarks).SemiBold();
               });

               // 3. The Mandatory DepEd Grading Legend
               column.Item().PaddingTop(15).Table(table =>
               {
                  table.ColumnsDefinition(columns =>
                  {
                     columns.RelativeColumn();
                     columns.RelativeColumn();
                     columns.RelativeColumn();
                  });

                  table.Header(header =>
                  {
                     header.Cell().Padding(2).Text("Descriptors").Bold();
                     header.Cell().Padding(2).Text("Grading Scale").Bold();
                     header.Cell().Padding(2).Text("Remarks").Bold();
                  });

                  void AddLegendRow(string desc, string scale, string remark)
                  {
                     table.Cell().Padding(2).Text(desc);
                     table.Cell().Padding(2).Text(scale);
                     table.Cell().Padding(2).Text(remark);
                  }

                  AddLegendRow("Outstanding", "90-100", "Passed");
                  AddLegendRow("Very Satisfactory", "85-89", "Passed");
                  AddLegendRow("Satisfactory", "80-84", "Passed");
                  AddLegendRow("Fairly Satisfactory", "75-79", "Passed");
                  AddLegendRow("Did Not Meet Expectations", "Below 75", "Failed");
               });

               // Force a page break for the back of the card
               column.Item().PageBreak();

               // ==========================================
               // PAGE 2: VALUES, ATTENDANCE & SIGNATURES
               // ==========================================

               column.Item().PaddingTop(10).Text("REPORT ON LEARNER'S OBSERVED VALUES").FontSize(12).Bold().AlignCenter();

               // 4. Core Values Grid
               column.Item().Table(table =>
               {
                  table.ColumnsDefinition(columns =>
                  {
                     columns.RelativeColumn(1.5f); // Core Value
                     columns.RelativeColumn(5);    // Behavior Statements
                     columns.RelativeColumn(0.6f); // Q1
                     columns.RelativeColumn(0.6f); // Q2
                     columns.RelativeColumn(0.6f); // Q3
                     columns.RelativeColumn(0.6f); // Q4
                  });

                  // Styles to match the exact centering and borders of the image
                  static IContainer HeaderStyle(IContainer c) => c.Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().AlignMiddle();
                  static IContainer CellStyle(IContainer c) => c.Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().AlignMiddle();

                  table.Header(header =>
                  {
                     // Row 1 of Header
                     header.Cell().RowSpan(2).Element(HeaderStyle).Text("Core Values").SemiBold();
                     header.Cell().RowSpan(2).Element(HeaderStyle).Text("Behavior Statements").SemiBold();
                     header.Cell().ColumnSpan(4).Element(HeaderStyle).Text("Quarter").SemiBold();

                     // Row 2 of Header (The numbers under 'Quarter')
                     header.Cell().Element(HeaderStyle).Text("1").SemiBold();
                     header.Cell().Element(HeaderStyle).Text("2").SemiBold();
                     header.Cell().Element(HeaderStyle).Text("3").SemiBold();
                     header.Cell().Element(HeaderStyle).Text("4").SemiBold();
                  });

                  // Helper to quickly draw the 4 empty quarter boxes
                  void DrawEmptyQuarters()
                  {
                     table.Cell().Element(CellStyle);
                     table.Cell().Element(CellStyle);
                     table.Cell().Element(CellStyle);
                     table.Cell().Element(CellStyle);
                  }

                  // 1. Maka-Diyos (Merges 2 rows vertically)
                  table.Cell().RowSpan(2).Element(CellStyle).Text("1.\nMaka-Diyos");
                  table.Cell().Element(CellStyle).Text("Expresses one's spiritual beliefs while respecting the spiritual beliefs of others");
                  DrawEmptyQuarters();
                  
                  table.Cell().Element(CellStyle).Text("Shows adherence to ethical principles by upholding truth");
                  DrawEmptyQuarters();

                  // 2. Makatao (Merges 2 rows vertically)
                  table.Cell().RowSpan(2).Element(CellStyle).Text("2.\nMakatao");
                  table.Cell().Element(CellStyle).Text("Is sensitive to individual, social, and cultural differences");
                  DrawEmptyQuarters();

                  table.Cell().Element(CellStyle).Text("Demonstrates contributions toward solidarity");
                  DrawEmptyQuarters();

                  // 3. Makakalikasan (Only 1 row, no merging needed)
                  table.Cell().Element(CellStyle).Text("3.\nMakakalikasan");
                  table.Cell().Element(CellStyle).Text("Cares for the environment and utilizes resources wisely, judiciously, and economically");
                  DrawEmptyQuarters();

                  // 4. Makabansa (Merges 2 rows vertically)
                  table.Cell().RowSpan(2).Element(CellStyle).Text("4.\nMakabansa");
                  table.Cell().Element(CellStyle).Text("Demonstrates pride in being a Filipino; exercises the rights and responsibilities of a Filipino Citizen");
                  DrawEmptyQuarters();

                  table.Cell().Element(CellStyle).Text("Demonstrates appropriate behavior in carrying out activities in the school, community, and country");
                  DrawEmptyQuarters();
               });

               // 5. Values Legend
               column.Item().PaddingTop(5).AlignCenter().Text("Marking: Non-numerical Rating (AO - Always Observed, SO - Sometimes Observed, RO - Rarely Observed, NO - Not Observed)").FontSize(9).Italic();

               column.Item().PaddingTop(20).Text("REPORT ON ATTENDANCE").FontSize(12).Bold().AlignCenter();

               // 6. Attendance Grid
               column.Item().Table(table =>
               {
                  table.ColumnsDefinition(columns =>
                  {
                     columns.RelativeColumn(2); // Title
                     for(int i=0; i<10; i++) columns.RelativeColumn(1); // 10 months
                     columns.RelativeColumn(1.2f); // Total
                  });

                  static IContainer AttStyle(IContainer c) => c.Border(1).BorderColor(Colors.Black).Padding(3).AlignCenter().AlignMiddle();

                  // Calculate the grand totals for the far-right column
                  int totalSchoolDays = vm.Sf9Attendance.Sum(a => a.SchoolDays);
                  int totalPresent = vm.Sf9Attendance.Sum(a => a.DaysPresent);
                  int totalAbsent = vm.Sf9Attendance.Sum(a => a.DaysAbsent);

                  // Draw Header
                  table.Header(header =>
                  {
                     header.Cell().Element(AttStyle).Text(""); // Empty corner box
                     foreach (var m in vm.Sf9Attendance)
                     {
                           header.Cell().Element(AttStyle).Text(m.Month).FontSize(9).SemiBold();
                     }
                     header.Cell().Element(AttStyle).Text("Total").FontSize(9).SemiBold();
                  });

                  // Foolproof Helper using IEnumerable<int> instead of explicitly naming the nested class
                  void AddAttendanceRow(string title, System.Collections.Generic.IEnumerable<int> monthlyValues, int total)
                  {
                     table.Cell().Element(AttStyle).AlignLeft().Text(title).FontSize(9);
                     foreach (int val in monthlyValues)
                     {
                           // If the value is 0 (like in future months), it prints blank so the card stays clean!
                           table.Cell().Element(AttStyle).Text(val > 0 ? val.ToString() : "").FontSize(9); 
                     }
                     table.Cell().Element(AttStyle).Text(total > 0 ? total.ToString() : "").FontSize(9).SemiBold();
                  }

                  AddAttendanceRow("No. of School Days", vm.Sf9Attendance.Select(m => m.SchoolDays), totalSchoolDays);
                  AddAttendanceRow("No. of Days Present", vm.Sf9Attendance.Select(m => m.DaysPresent), totalPresent);
                  AddAttendanceRow("No. of Days Absent", vm.Sf9Attendance.Select(m => m.DaysAbsent), totalAbsent);
               });

               // 7. Certificate of Transfer & Signatures
               column.Item().PaddingTop(30).Column(sigCol =>
               {
                  sigCol.Item().Text("CERTIFICATE OF TRANSFER").FontSize(11).Bold();
                  sigCol.Item().PaddingTop(5).Text(x =>
                  {
                     x.Span("Admitted to Grade: ");
                     x.Span("________________________").Underline();
                     x.Span("    Section: ");
                     x.Span("________________________").Underline();
                     x.Span("    Eligible for Admission to Grade: ");
                     x.Span("________________________").Underline();
                  });
                  
                  sigCol.Item().PaddingTop(25).Row(row =>
                  {
                     row.RelativeItem().Column(c =>
                     {
                           c.Item().Text("_________________________________").AlignCenter();
                           c.Item().Text("Teacher / Adviser").AlignCenter();
                     });
                     row.RelativeItem().Column(c =>
                     {
                           c.Item().Text("_________________________________").AlignCenter();
                           c.Item().Text("Principal").AlignCenter();
                     });
                  });

                  sigCol.Item().PaddingTop(30).Text("PARENT/GUARDIAN SIGNATURE").FontSize(11).Bold();
                  
                  void AddSignatureLine(string quarter)
                  {
                     sigCol.Item().PaddingTop(15).Row(row =>
                     {
                           row.ConstantItem(100).Text($"{quarter} Quarter:");
                           row.RelativeItem().Text("________________________________________________");
                     });
                  }

                  AddSignatureLine("1st");
                  AddSignatureLine("2nd");
                  AddSignatureLine("3rd");
                  AddSignatureLine("4th");
               });
         });
      }
   }
}