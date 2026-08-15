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
      // 1. Core Calculator & String Builder
      private (double RawGrade, string Breakdown) GetTermGradeWithDetails(StudentGradeRow row, string targetPeriod)
      {
         var termAssessments = ClassAssessments.Where(a => a.GradingPeriod == targetPeriod).ToList();
         var rawScores = row.Scores.Values.Select(v => v.DbModel).ToList();

         if (!AvailableCategories.Any() || !termAssessments.Any())
            return (0, "No assessments recorded yet.");

         double totalRawContribution = 0;
         double totalExcelWS = 0;
         double totalPolicyWeight = 0;
         
         var tip = new StringBuilder();
         tip.AppendLine($"[ {targetPeriod} Score Breakdown ]");

         foreach (var category in AvailableCategories)
         {
            double weightDec = category.Weight / 100.0;
            totalPolicyWeight += weightDec; 

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
               // Centriku Raw Math
               double rawPct = (earned / max) * 100.0;
               double rawContribution = rawPct * weightDec;
               totalRawContribution += rawContribution;

               // Excel TS/WS Math
               double ts = rawPct;
               ts = (rawPct / 100.0) * (100.0 - this.NrfgBaseValue) + this.NrfgBaseValue;
               double ws = ts * weightDec;
               totalExcelWS += ws;

               tip.AppendLine($"• {category.Name} ({category.Weight}% weight):");
               tip.AppendLine($"   Earned: {earned:0.##} / {max:0.##} = {rawPct:0.##}%");
               
               tip.AppendLine($"   Excel TS (Base-{this.NrfgBaseValue}): {ts:0.##}");
               tip.AppendLine($"   Excel WS: {ts:0.##} × {weightDec:0.##} = {ws:0.##}");
            }
            else
            {
               tip.AppendLine($"• {category.Name} ({category.Weight}% weight):");
               tip.AppendLine($"   No assessments. Excel TS: 0.00 | Excel WS: 0.00");
            }
         }

         // Calculate the Original Centriku Running Average (e.g. 5%)
         double finalRaw = totalPolicyWeight > 0 ? totalRawContribution / totalPolicyWeight : 0;

         tip.AppendLine($"--------------------------");
         tip.AppendLine($"Excel Term Grade (Sum of WS): {totalExcelWS:0.##}");
         tip.AppendLine($"Centriku Term Raw Grade: {finalRaw:0.##}%");

         return (finalRaw, tip.ToString().TrimEnd());
      }

      // 2. Transmutation Explanation Builder
      private string GetTransmutationExplanation(double raw)
      {
          double nrfgBase = this.NrfgBaseValue;
          double transmuted = (raw / 100.0) * (100.0 - nrfgBase) + nrfgBase;
          return $"\n[ Centriku Running Average (NRFG) ]\nFormula: ({raw:0.##}% / 100) × (100 - {nrfgBase}) + {nrfgBase}\nFinal UI Display: {transmuted:0.##}%";
      }

      public void RecalculateFinalGrades() => RecalculateFinalGradesForList(GradebookRows, AttendanceGridRows);

      public void RecalculateFinalGradesForList(System.Collections.Generic.IEnumerable<StudentGradeRow> targetGradeRows, System.Collections.Generic.IEnumerable<AttendanceGridRowViewModel> targetAttRows)
      {
         if (targetGradeRows == null || targetAttRows == null) return;

         var classCfg = new TeacherClass { AttendanceCalculationMode = this.AttendanceCalculationMode, MaxAbsencesAllowed = this.MaxAbsencesAllowed, AttendanceWeight = this.AttendanceWeight, LateValue = this.LateValue };
         var tempCfg = new GradingTemplate { NrfgBaseValue = this.NrfgBaseValue, PassingGrade = 75.0 };

         foreach (var row in targetGradeRows)
         {
            // === 1. MIDTERM & FINAL MATH ===
            bool hasMidterm = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Midterm");
            var midResult = GetTermGradeWithDetails(row, "Midterm");
            row.MidtermGradeNumeric = midResult.RawGrade;
            var tempMidResult = GradeCalculationService.EvaluateFinalGrade(midResult.RawGrade, new TeacherClass { AttendanceCalculationMode = "None" }, tempCfg, 0, 0);
            row.MidtermGradeDisplay = hasMidterm ? tempMidResult.FinalOutput : "--";
            row.MidtermComputationTooltip = hasMidterm ? $"{midResult.Breakdown}\n{GetTransmutationExplanation(midResult.RawGrade)}" : "No Midterm Assessments";

            bool hasFinal = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Final");
            var finResult = GetTermGradeWithDetails(row, "Final");
            row.FinalTermGradeNumeric = finResult.RawGrade;
            var tempFinResult = GradeCalculationService.EvaluateFinalGrade(finResult.RawGrade, new TeacherClass { AttendanceCalculationMode = "None" }, tempCfg, 0, 0);
            row.FinalTermGradeDisplay = hasFinal ? tempFinResult.FinalOutput : "--";
            row.FinalComputationTooltip = hasFinal ? $"{finResult.Breakdown}\n{GetTransmutationExplanation(finResult.RawGrade)}" : "No Final Assessments";

            // === 2. SEMESTER MATH ===
            double finalAcademicGrade = 100.0;
            bool isFinalMathComplete = false; 
            string baseMathTooltip = "";

            if (SelectedTermView == "Semester Average")
            {
               if (hasMidterm && hasFinal) 
               { 
                  finalAcademicGrade = (midResult.RawGrade + finResult.RawGrade) / 2.0; 
                  isFinalMathComplete = true; 
                  baseMathTooltip = $"[ Semester Average ]\nMidterm Score: {midResult.RawGrade:0.##}%\nFinal Score: {finResult.RawGrade:0.##}%\nCalculation: ({midResult.RawGrade:0.##} + {finResult.RawGrade:0.##}) / 2\nSemester Academic Grade: {finalAcademicGrade:0.##}%\n";
               }
               else baseMathTooltip = "Requires both Midterm and Final scores to compute Semester Average.";
            }
            else
            {
               finalAcademicGrade = SelectedTermView switch { "Midterm" => midResult.RawGrade, "Final" => finResult.RawGrade, _ => 100.0 };
               isFinalMathComplete = SelectedTermView switch { "Midterm" => hasMidterm, "Final" => hasFinal, _ => false };
               string detailedBreakdown = SelectedTermView switch { "Midterm" => midResult.Breakdown, "Final" => finResult.Breakdown, _ => "" };
               baseMathTooltip = $"{detailedBreakdown}\n";
            }

            // === 3. ATTENDANCE & FINAL OUTPUT ===
            if (!isFinalMathComplete)
            {
               row.FinalGrade = "--";
               row.FinalGradeNumeric = 0;
               row.FinalGradeTooltip = baseMathTooltip;
            }
            else
            {
               var attRow = targetAttRows.FirstOrDefault(a => a.StudentInfo.StudentID == row.StudentID);
               int totalDays = attRow?.Cells.Count ?? 0;
               int excusedDays = attRow?.TotalE ?? 0;
               int activeDays = totalDays - excusedDays;
               double effectiveAbsences = (attRow?.TotalA ?? 0) + ((attRow?.TotalL ?? 0) * LateValue);

               var finalEval = GradeCalculationService.EvaluateFinalGrade(finalAcademicGrade, classCfg, tempCfg, activeDays, effectiveAbsences);

               row.FinalGrade = finalEval.FinalOutput;
               row.FinalGradeNumeric = finalEval.FinalNumeric;
               
               string attString = "";
               if (AttendanceCalculationMode != "None")
               {
                  attString = $"\n[ Attendance Modifier: {AttendanceCalculationMode} ]\nTotal Active Days: {activeDays}\nEffective Absences: {effectiveAbsences} (A + L*{LateValue})\n";
                  if (AttendanceCalculationMode == "Weighted") attString += $"Academic Weight: {100-AttendanceWeight}%, Attendance Weight: {AttendanceWeight}%\n";
                  if (AttendanceCalculationMode == "Bonus") attString += $"Bonus/Penalty Applied: {AttendanceWeight}%\n";
                  if (AttendanceCalculationMode == "Threshold") attString += $"Max Absences Allowed: {MaxAbsencesAllowed}\n";
               }

               row.FinalGradeTooltip = $"{baseMathTooltip}{attString}{GetTransmutationExplanation(finalEval.RawAcademicGrade)}";
               if (finalEval.IsFA) row.FinalGradeTooltip += "\n\n⚠️ STATUS: Failed due to Absences (FA)";
            }
         }
      }
   }
}