using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels
{
   public partial class GradebookViewModel
   {
      // 1. Core Calculator & String Builder (EXCEL TEMPLATE MATH)
      private (double TermGrade, string Breakdown) GetTermGradeWithDetails(StudentGradeRow row, string targetPeriod)
      {
         var termAssessments = ClassAssessments.Where(a => a.GradingPeriod == targetPeriod).ToList();
         var rawScores = row.Scores.Values.Select(v => v.DbModel).ToList();

         if (!AvailableCategories.Any() || !termAssessments.Any())
            return (-1, "No assessments recorded yet."); // -1 = Incomplete

         double totalExcelWS = 0;
         bool isMissingCategory = false; // The flag for incomplete categories
         
         var tip = new StringBuilder();
         tip.AppendLine($"[ {targetPeriod} Score Breakdown ]");

         foreach (var category in AvailableCategories)
         {
            // Ensure the dictionary entry exists for binding
            if (!row.CategoryGrades.ContainsKey(category.SequenceOrder))
               row.CategoryGrades[category.SequenceOrder] = new CategoryGradeViewModel();
            var catGrade = row.CategoryGrades[category.SequenceOrder];

            double weightDec = category.Weight / 100.0;
            var catAssessments = termAssessments.Where(a => a.Category == category.Name).ToList();
            
            double earned = 0, max = 0;
            foreach (var a in catAssessments)
            {
               var score = rawScores.FirstOrDefault(s => s.AssessmentID == a.AssessmentID);
               if (score != null && !score.IsExcused && a.MaxScore > 0)
               {
                  earned += score.PointsEarned;
                  max += a.MaxScore;
               }
            }

            if (max > 0)
            {
               double ts = Math.Round((earned / max) * (100.0 - this.NrfgBaseValue) + this.NrfgBaseValue, 2, MidpointRounding.AwayFromZero);
               double ws = Math.Round(ts * weightDec, 2, MidpointRounding.AwayFromZero);
               totalExcelWS += ws;

               // Save the TS and WS logic to the specific category cells
               catGrade.TsDisplay = $"{ts:0.00}";
               catGrade.WsDisplay = $"{ws:0.00}";
               catGrade.TsTooltip = $"Formula: (Earned ÷ Max) × (100 - Base) + Base\nMath: ({earned:0.##} ÷ {max:0.##}) × {100 - this.NrfgBaseValue} + {this.NrfgBaseValue} = {ts:0.00}";
               catGrade.WsTooltip = $"Formula: TS × Category Weight\nMath: {ts:0.00} × {weightDec:0.##} = {ws:0.00}";

               tip.AppendLine($"• {category.Name} ({category.Weight}%): TS = {ts:0.00}, WS = {ws:0.00}");
            }
            else
            {
               isMissingCategory = true; // Trigger the block!
               
               catGrade.TsDisplay = "--";
               catGrade.WsDisplay = "--";
               catGrade.TsTooltip = "Missing assessments for this category.";
               catGrade.WsTooltip = "Missing assessments for this category.";
               
               tip.AppendLine($"• {category.Name} ({category.Weight}%): INCOMPLETE");
            }
         }

         // NEW: If any category is empty, we return -1 so the term grade stays blank!
         if (isMissingCategory)
             return (-1, "Incomplete: One or more categories are missing assessments.");

         double finalTermGrade = Math.Round(totalExcelWS, 0, MidpointRounding.AwayFromZero);
         tip.AppendLine($"--------------------------");
         tip.AppendLine($"Term Grade (Sum of WS): {totalExcelWS:0.00}");
         tip.AppendLine($"Rounded Term Grade: {finalTermGrade}%");

         return (finalTermGrade, tip.ToString().TrimEnd());
      }

      public void RecalculateFinalGrades() => RecalculateFinalGradesForList(GradebookRows, AttendanceGridRows);

      public void RecalculateFinalGradesForList(System.Collections.Generic.IEnumerable<StudentGradeRow> targetGradeRows, System.Collections.Generic.IEnumerable<AttendanceGridRowViewModel> targetAttRows)
      {
         if (targetGradeRows == null || targetAttRows == null) return;

         foreach (var row in targetGradeRows)
         {
            // === 1. MIDTERM & FINAL MATH ===
            bool hasMidterm = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Midterm");
            var midResult = GetTermGradeWithDetails(row, "Midterm");
            bool isMidComplete = midResult.TermGrade >= 0;
            
            row.MidtermGradeNumeric = isMidComplete ? midResult.TermGrade : 0;
            row.MidtermGradeDisplay = isMidComplete ? $"{midResult.TermGrade}%" : "--";
            row.MidtermComputationTooltip = midResult.Breakdown;

            bool hasFinal = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Final");
            var finResult = GetTermGradeWithDetails(row, "Final");
            bool isFinComplete = finResult.TermGrade >= 0;
            
            row.FinalTermGradeNumeric = isFinComplete ? finResult.TermGrade : 0;
            row.FinalTermGradeDisplay = isFinComplete ? $"{finResult.TermGrade}%" : "--";
            row.FinalComputationTooltip = finResult.Breakdown;

            // === 2. SEMESTER MATH ===
            double finalAcademicGrade = 100.0;
            bool isFinalMathComplete = false; 
            string baseMathTooltip = "";

            if (SelectedTermView == "Semester Average")
            {
               if (isMidComplete && isFinComplete) 
               { 
                  finalAcademicGrade = Math.Round((midResult.TermGrade + finResult.TermGrade) / 2.0, 0, MidpointRounding.AwayFromZero); 
                  isFinalMathComplete = true; 
                  baseMathTooltip = $"[ Semester Average ]\nMidterm Score: {midResult.TermGrade}%\nFinal Score: {finResult.TermGrade}%\nCalculation: ({midResult.TermGrade} + {finResult.TermGrade}) / 2\nSemester Academic Grade: {finalAcademicGrade}%\n";
               }
               else baseMathTooltip = "Requires both Midterm and Final term grades to be complete.";
            }
            else
            {
               finalAcademicGrade = SelectedTermView switch { "Midterm" => midResult.TermGrade, "Final" => finResult.TermGrade, _ => 100.0 };
               isFinalMathComplete = SelectedTermView switch { "Midterm" => isMidComplete, "Final" => isFinComplete, _ => false };
               baseMathTooltip = SelectedTermView switch { "Midterm" => midResult.Breakdown, "Final" => finResult.Breakdown, _ => "" };
            }

            // === 3. FINAL OUTPUT ===
            if (!isFinalMathComplete)
            {
               row.FinalGrade = "--";
               row.FinalGradeNumeric = 0;
               row.FinalGradeTooltip = baseMathTooltip;
            }
            else
            {
               row.FinalGrade = $"{finalAcademicGrade}%";
               row.FinalGradeNumeric = finalAcademicGrade;
               row.FinalGradeTooltip = baseMathTooltip;
            }
         }
      }
   }
}