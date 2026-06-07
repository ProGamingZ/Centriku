using System;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Centriku.ViewModels;
using Centriku.Models;

namespace Centriku.Services
{
   public class Sf9Generator
   {
      public static void GenerateReportCard(DirectoryViewModel vm, string destinationPath, AppSettings settings)
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

                  page.Header().Element(c => ComposeHeader(c, settings));
                  page.Content().Element(c => ComposeContent(c, vm, settings));
                  
                  page.Footer().AlignCenter().Text(x =>
                  {
                     x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages();
                  });
               });
         });

         document.GeneratePdf(destinationPath);
      }

      private static void ComposeHeader(IContainer container, AppSettings settings)
      {
         container.Row(row =>
         {
               row.RelativeItem().Column(column =>
               {
                  column.Item().Text("Republic of the Philippines").FontSize(10).AlignCenter();
                  column.Item().Text("Department of Education").FontSize(14).Bold().AlignCenter();
                  
                  // Inject Custom Region and Division
                  if (!string.IsNullOrWhiteSpace(settings.Region))
                      column.Item().Text(settings.Region).FontSize(10).AlignCenter();
                  if (!string.IsNullOrWhiteSpace(settings.Division))
                      column.Item().Text($"Division of {settings.Division}").FontSize(10).AlignCenter();

                  // Inject the Custom School Name
                  string title = string.IsNullOrWhiteSpace(settings.SchoolName) ? "Learner Progress Report Card (SF9)" : settings.SchoolName;
                  column.Item().PaddingTop(5).Text(title).FontSize(14).Bold().AlignCenter();
                  
                  // Inject School ID and District
                  if (!string.IsNullOrWhiteSpace(settings.SchoolId) || !string.IsNullOrWhiteSpace(settings.District))
                  {
                      string subHeader = "";
                      if (!string.IsNullOrWhiteSpace(settings.SchoolId)) subHeader += $"School ID: {settings.SchoolId}   ";
                      if (!string.IsNullOrWhiteSpace(settings.District)) subHeader += $"District: {settings.District}";
                      column.Item().Text(subHeader.Trim()).FontSize(10).AlignCenter();
                  }
               });
         });
      }

      private static void ComposeContent(IContainer container, DirectoryViewModel vm, AppSettings settings)
      {
         var student = vm.SelectedProfile!;

         // Local Helper to handle Blank Grades setting (Blank, Dash, or NA)
         string FormatGrade(string g) 
         {
             if (string.IsNullOrWhiteSpace(g) || g == "--") 
             {
                 return settings.BlankGradeOutput switch {
                     "Dash" => "--",
                     "NA" => "N/A",
                     _ => ""
                 };
             }
             return g;
         }

         container.PaddingVertical(1, Unit.Centimetre).Column(column =>
         {
               column.Spacing(10);
               
               //  1. Demographics Grid 
               column.Item().Table(table =>
               {
                  table.ColumnsDefinition(cols => { cols.RelativeColumn(3); cols.RelativeColumn(1); });
                  table.Cell().Text($"Name: {student.LastName}, {student.FirstName} {student.MiddleName}").Bold();
                  table.Cell().Text($"LRN: {student.StudentID}");
                  table.Cell().Text($"Grade & Section: {student.GradeYearLevel} - {student.SectionBlock}");
                  table.Cell().Text($"Sex: {student.Gender}");
               });
               
               column.Item().PaddingTop(10).Text("REPORT ON LEARNING PROGRESS AND ACHIEVEMENT").FontSize(12).Bold().AlignCenter();

               //  2. Master Academic Grid 
               column.Item().Table(table =>
               {
                  table.ColumnsDefinition(cols => {
                     cols.RelativeColumn(3); cols.RelativeColumn(1); cols.RelativeColumn(1); 
                     cols.RelativeColumn(1); cols.RelativeColumn(1); cols.RelativeColumn(1.5f); cols.RelativeColumn(1.5f); 
                  });

                  static IContainer HeaderStyle(IContainer c) => c.Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().AlignMiddle();
                  static IContainer CellStyle(IContainer c) => c.Border(1).BorderColor(Colors.Black).Padding(4);

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

                  foreach (var row in vm.MasterGrades)
                  {
                     table.Cell().Element(CellStyle).Text(row.SubjectName);
                     table.Cell().Element(CellStyle).AlignCenter().Text(FormatGrade(row.Q1Text));
                     table.Cell().Element(CellStyle).AlignCenter().Text(FormatGrade(row.Q2Text));
                     table.Cell().Element(CellStyle).AlignCenter().Text(FormatGrade(row.Q3Text));
                     table.Cell().Element(CellStyle).AlignCenter().Text(FormatGrade(row.Q4Text));
                     table.Cell().Element(CellStyle).AlignCenter().Text(FormatGrade(row.FinalGrade)).SemiBold();
                     
                     // Custom Threshold Check for Remarks
                     string remarks = FormatGrade(row.Remarks);
                     if (double.TryParse(row.FinalGrade, out double fg)) { remarks = fg >= settings.PassingGradeThreshold ? "Passed" : "Failed"; }
                     table.Cell().Element(CellStyle).AlignCenter().Text(remarks);
                  }

                  string generalRemarks = FormatGrade("--");
                  if (vm.FinalGeneralAverage != "--" && double.TryParse(vm.FinalGeneralAverage, out double fga))
                  {
                     generalRemarks = fga >= settings.PassingGradeThreshold ? "Passed" : "Failed";
                  }

                  table.Cell().Element(CellStyle).AlignRight().Text("General Average").SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(FormatGrade(vm.Q1Average)).SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(FormatGrade(vm.Q2Average)).SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(FormatGrade(vm.Q3Average)).SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(FormatGrade(vm.Q4Average)).SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(FormatGrade(vm.FinalGeneralAverage)).SemiBold();
                  table.Cell().Element(CellStyle).AlignCenter().Text(generalRemarks).SemiBold();
               });

               //  3. DepEd Grading Legend 
               column.Item().PaddingTop(15).Table(table =>
               {
                  table.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(); cols.RelativeColumn(); });
                  table.Header(header => {
                     header.Cell().Padding(2).Text("Descriptors").Bold();
                     header.Cell().Padding(2).Text("Grading Scale").Bold();
                     header.Cell().Padding(2).Text("Remarks").Bold();
                  });

                  void AddLegendRow(string desc, string scale, string remark) {
                     table.Cell().Padding(2).Text(desc); table.Cell().Padding(2).Text(scale); table.Cell().Padding(2).Text(remark);
                  }

                  AddLegendRow(settings.LegDesc1 ?? "", settings.LegScale1 ?? "", settings.LegRem1 ?? "");
                  AddLegendRow(settings.LegDesc2 ?? "", settings.LegScale2 ?? "", settings.LegRem2 ?? "");
                  AddLegendRow(settings.LegDesc3 ?? "", settings.LegScale3 ?? "", settings.LegRem3 ?? "");
                  AddLegendRow(settings.LegDesc4 ?? "", settings.LegScale4 ?? "", settings.LegRem4 ?? "");
                  AddLegendRow(settings.LegDesc5 ?? "", settings.LegScale5 ?? "", settings.LegRem5 ?? "");
               });

               column.Item().PageBreak(); // Flip to page 2

               //  4. Core Values Grid 
               column.Item().PaddingTop(10).Text("REPORT ON LEARNER'S OBSERVED VALUES").FontSize(12).Bold().AlignCenter();
               column.Item().Table(table =>
               {
                  table.ColumnsDefinition(cols => {
                     cols.RelativeColumn(1.5f); cols.RelativeColumn(5); 
                     cols.RelativeColumn(0.6f); cols.RelativeColumn(0.6f); cols.RelativeColumn(0.6f); cols.RelativeColumn(0.6f);
                  });

                  static IContainer HStyle(IContainer c) => c.Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().AlignMiddle();
                  static IContainer CStyle(IContainer c) => c.Border(1).BorderColor(Colors.Black).Padding(4).AlignCenter().AlignMiddle();

                  table.Header(header => {
                     header.Cell().RowSpan(2).Element(HStyle).Text("Core Values").SemiBold();
                     header.Cell().RowSpan(2).Element(HStyle).Text("Behavior Statements").SemiBold();
                     header.Cell().ColumnSpan(4).Element(HStyle).Text("Quarter").SemiBold();
                     header.Cell().Element(HStyle).Text("1").SemiBold(); header.Cell().Element(HStyle).Text("2").SemiBold();
                     header.Cell().Element(HStyle).Text("3").SemiBold(); header.Cell().Element(HStyle).Text("4").SemiBold();
                  });

                  void DrawEmpty() { table.Cell().Element(CStyle); table.Cell().Element(CStyle); table.Cell().Element(CStyle); table.Cell().Element(CStyle); }

                  table.Cell().RowSpan(2).Element(CStyle).Text("1.\nMaka-Diyos");
                  table.Cell().Element(CStyle).Text("Expresses one's spiritual beliefs while respecting the spiritual beliefs of others"); DrawEmpty();
                  table.Cell().Element(CStyle).Text("Shows adherence to ethical principles by upholding truth"); DrawEmpty();

                  table.Cell().RowSpan(2).Element(CStyle).Text("2.\nMakatao");
                  table.Cell().Element(CStyle).Text("Is sensitive to individual, social, and cultural differences"); DrawEmpty();
                  table.Cell().Element(CStyle).Text("Demonstrates contributions toward solidarity"); DrawEmpty();

                  table.Cell().Element(CStyle).Text("3.\nMakakalikasan");
                  table.Cell().Element(CStyle).Text("Cares for the environment and utilizes resources wisely, judiciously, and economically"); DrawEmpty();

                  table.Cell().RowSpan(2).Element(CStyle).Text("4.\nMakabansa");
                  table.Cell().Element(CStyle).Text("Demonstrates pride in being a Filipino; exercises the rights and responsibilities of a Filipino Citizen"); DrawEmpty();
                  table.Cell().Element(CStyle).Text("Demonstrates appropriate behavior in carrying out activities in the school, community, and country"); DrawEmpty();
               });

               column.Item().PaddingTop(5).AlignCenter().Text("Marking: Non-numerical Rating (AO - Always Observed, SO - Sometimes Observed, RO - Rarely Observed, NO - Not Observed)").FontSize(9).Italic();

               //  5. Attendance Grid 
               column.Item().PaddingTop(20).Text("REPORT ON ATTENDANCE").FontSize(12).Bold().AlignCenter();
               column.Item().Table(table =>
               {
                  table.ColumnsDefinition(cols => {
                     cols.RelativeColumn(2); 
                     for(int i=0; i<10; i++) cols.RelativeColumn(1); 
                     cols.RelativeColumn(1.2f); 
                  });

                  static IContainer AttStyle(IContainer c) => c.Border(1).BorderColor(Colors.Black).Padding(3).AlignCenter().AlignMiddle();

                  int totalSchoolDays = vm.Sf9Attendance.Sum(a => a.SchoolDays);
                  int totalPresent = vm.Sf9Attendance.Sum(a => a.DaysPresent);
                  int totalAbsent = vm.Sf9Attendance.Sum(a => a.DaysAbsent);

                  table.Header(header => {
                     header.Cell().Element(AttStyle).Text("");
                     foreach (var m in vm.Sf9Attendance) header.Cell().Element(AttStyle).Text(m.Month).FontSize(9).SemiBold();
                     header.Cell().Element(AttStyle).Text("Total").FontSize(9).SemiBold();
                  });

                  void AddAttendanceRow(string title, System.Collections.Generic.IEnumerable<int> values, int total) {
                     table.Cell().Element(AttStyle).AlignLeft().Text(title).FontSize(9);
                     foreach (int val in values) table.Cell().Element(AttStyle).Text(val > 0 ? val.ToString() : "").FontSize(9); 
                     table.Cell().Element(AttStyle).Text(total > 0 ? total.ToString() : "").FontSize(9).SemiBold();
                  }

                  AddAttendanceRow("No. of School Days", vm.Sf9Attendance.Select(m => m.SchoolDays), totalSchoolDays);
                  AddAttendanceRow("No. of Days Present", vm.Sf9Attendance.Select(m => m.DaysPresent), totalPresent);
                  AddAttendanceRow("No. of Days Absent", vm.Sf9Attendance.Select(m => m.DaysAbsent), totalAbsent);
               });

               //  6. Signatories (INJECTING THE NEW SETTINGS) 
               string teacherName = string.IsNullOrWhiteSpace(settings.DefaultTeacherName) ? "_________________________________" : settings.DefaultTeacherName;
               string principalName = string.IsNullOrWhiteSpace(settings.PrincipalName) ? "_________________________________" : settings.PrincipalName;
               string principalTitle = string.IsNullOrWhiteSpace(settings.PrincipalTitle) ? "Principal" : settings.PrincipalTitle;

               column.Item().PaddingTop(30).Column(sigCol =>
               {
                  sigCol.Item().Text("CERTIFICATE OF TRANSFER").FontSize(11).Bold();
                  sigCol.Item().PaddingTop(5).Text(x =>
                  {
                     x.Span("Admitted to Grade: "); x.Span("________________________").Underline();
                     x.Span("    Section: "); x.Span("________________________").Underline();
                     x.Span("    Eligible for Admission to Grade: "); x.Span("________________________").Underline();
                  });
                  
                  sigCol.Item().PaddingTop(25).Row(row =>
                  {
                     row.RelativeItem().Column(c =>
                     {
                           c.Item().Text(teacherName).Bold().AlignCenter().Underline();
                           c.Item().Text("Teacher / Adviser").AlignCenter();
                     });
                     row.RelativeItem().Column(c =>
                     {
                           c.Item().Text(principalName).Bold().AlignCenter().Underline();
                           c.Item().Text(principalTitle).AlignCenter();
                     });
                  });

                  sigCol.Item().PaddingTop(30).Text("PARENT/GUARDIAN SIGNATURE").FontSize(11).Bold();
                  
                  void AddSignatureLine(string quarter) {
                     sigCol.Item().PaddingTop(15).Row(row => {
                           row.ConstantItem(100).Text($"{quarter} Quarter:");
                           row.RelativeItem().Text("________________________________________________");
                     });
                  }

                  AddSignatureLine("1st"); AddSignatureLine("2nd"); AddSignatureLine("3rd"); AddSignatureLine("4th");
               });
         });
      }
   }
}