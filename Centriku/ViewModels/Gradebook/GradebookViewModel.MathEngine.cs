using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels
{
   public partial class GradebookViewModel
   {
      // Transforms UI Cells into Raw Scores for the Service 
      private double GetTermRawGrade(StudentGradeRow row, string targetPeriod)
      {
         var termAssessments = ClassAssessments.Where(a => a.GradingPeriod == targetPeriod).ToList();
         var rawScores = row.Scores.Values.Select(v => v.DbModel).ToList();
         
         return GradeCalculationService.CalculateRawAcademicGrade(AvailableCategories, termAssessments, rawScores);
      }

      // Formats individual terms (Q1, Q2) bypassing attendance penalties 
      private string GetTermDisplay(double rawGrade, bool hasAssessments)
      {
         if (!hasAssessments) return "--";

         var tempClass = new TeacherClass { AttendanceCalculationMode = "None" }; 
         var tempTemplate = new GradingTemplate { CalculationMode = this.CalculationMode, NrfgBaseValue = this.NrfgBaseValue };

         var result = GradeCalculationService.EvaluateFinalGrade(rawGrade, tempClass, tempTemplate, ClassGradeBoundaries, 0, 0);
         return result.FinalOutput;
      }
     
      public void RecalculateFinalGrades()
      {
         RecalculateFinalGradesForList(GradebookRows, AttendanceGridRows);
      }

      public void RecalculateFinalGradesForList(System.Collections.Generic.IEnumerable<StudentGradeRow> targetGradeRows, System.Collections.Generic.IEnumerable<AttendanceGridRowViewModel> targetAttRows)
      {
         if (targetGradeRows == null || targetAttRows == null) return;

         // Package the current Gradebook UI state into config objects for the Service
         var currentClassConfig = new TeacherClass 
         {
            AttendanceCalculationMode = this.AttendanceCalculationMode,
            MaxAbsencesAllowed = this.MaxAbsencesAllowed,
            AttendanceWeight = this.AttendanceWeight,
            LateValue = this.LateValue
         };

         var currentTemplateConfig = new GradingTemplate
         {
             CalculationMode = this.CalculationMode,
             NrfgBaseValue = this.NrfgBaseValue,
             PassingGrade = 75.0 // Safe default for the UI
         };

         foreach (var row in targetGradeRows)
         {
            // === 1. ALWAYS CALCULATE EVERY TERM ===
            bool hasMidterm = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Midterm");
            double midterm = GetTermRawGrade(row, "Midterm");
            row.MidtermGradeDisplay = GetTermDisplay(midterm, hasMidterm);
            row.MidtermGradeNumeric = midterm;

            bool hasFinal = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Final");
            double final = GetTermRawGrade(row, "Final");
            row.FinalTermGradeDisplay = GetTermDisplay(final, hasFinal);
            row.FinalTermGradeNumeric = final;

            bool hasQ1 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q1");
            double q1 = GetTermRawGrade(row, "Q1");
            row.Q1GradeDisplay = GetTermDisplay(q1, hasQ1);
            row.Q1GradeNumeric = q1;

            bool hasQ2 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q2");
            double q2 = GetTermRawGrade(row, "Q2");
            row.Q2GradeDisplay = GetTermDisplay(q2, hasQ2);
            row.Q2GradeNumeric = q2;

            bool hasQ3 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q3");
            double q3 = GetTermRawGrade(row, "Q3");
            row.Q3GradeDisplay = GetTermDisplay(q3, hasQ3);
            row.Q3GradeNumeric = q3;

            bool hasQ4 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q4");
            double q4 = GetTermRawGrade(row, "Q4");
            row.Q4GradeDisplay = GetTermDisplay(q4, hasQ4);
            row.Q4GradeNumeric = q4;

            // 2. DETERMINE THE 'ACTIVE' FINAL GRADE FOR THE UI 
            double finalAcademicGrade = 100.0;
            bool isFinalMathComplete = false; 

            if (SelectedTermView == "Semester Average")
            {
               if (hasMidterm && hasFinal) { finalAcademicGrade = (midterm + final) / 2.0; isFinalMathComplete = true; }
            }
            else if (SelectedTermView == "Final Average")
            {
               if (hasQ1 && hasQ2 && hasQ3 && hasQ4) { finalAcademicGrade = (q1 + q2 + q3 + q4) / 4.0; isFinalMathComplete = true; }
            }
            else
            {
               // If viewing a single term, grab its pre-calculated value
               finalAcademicGrade = SelectedTermView switch {
                  "Q1" => q1, "Q2" => q2, "Q3" => q3, "Q4" => q4, "Midterm" => midterm, "Final" => final, _ => 100.0
               };
               isFinalMathComplete = SelectedTermView switch {
                  "Q1" => hasQ1, "Q2" => hasQ2, "Q3" => hasQ3, "Q4" => hasQ4, "Midterm" => hasMidterm, "Final" => hasFinal, _ => false
               };
            }

            // 3. ATTENDANCE & FINAL TRANSMUTATION VIA THE SERVICE 
            if (!isFinalMathComplete)
            {
               row.FinalGrade = "--";
               row.FinalGradeNumeric = 0;
            }
            else
            {
               var attendanceRow = targetAttRows.FirstOrDefault(a => a.StudentInfo.StudentID == row.StudentID);
               int totalDays = attendanceRow?.Cells.Count ?? 0;
               int excusedDays = attendanceRow?.TotalE ?? 0;
               int activeDays = totalDays - excusedDays;
               double effectiveAbsences = (attendanceRow?.TotalA ?? 0) + ((attendanceRow?.TotalL ?? 0) * LateValue);

               // One clean call to the service!
               var finalResult = GradeCalculationService.EvaluateFinalGrade(
                  rawAcademicGrade: finalAcademicGrade,
                  teacherClass: currentClassConfig,
                  template: currentTemplateConfig,
                  boundaries: ClassGradeBoundaries,
                  totalActiveDays: activeDays,
                  effectiveAbsences: effectiveAbsences
               );

               row.FinalGrade = finalResult.FinalOutput;
               row.FinalGradeNumeric = finalResult.FinalNumeric;
            }
         }
      }
   }
}