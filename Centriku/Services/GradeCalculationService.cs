using System;
using System.Collections.Generic;
using System.Linq;
using Centriku.Models;

namespace Centriku.Services
{
   public class GradeResult
   {
      public double RawAcademicGrade { get; set; }
      public double FinalNumeric { get; set; }
      public string FinalOutput { get; set; } = string.Empty;
      public bool IsFailing { get; set; }
      public bool IsFA { get; set; } // Failed specifically due to Absences
   }

   public static class GradeCalculationService
   {
      /// Calculates the raw category-weighted academic grade.
      public static double CalculateRawAcademicGrade(
         IEnumerable<GradingCategory> categories,
         IEnumerable<Assessment> targetAssessments,
         IEnumerable<Score> studentScores)
      {
         double academicGrade = 0;
         double totalWeightedScore = 0;
         double totalCategoryWeight = 0;
         bool hasAnyAssessments = false;

         if (categories == null || !categories.Any()) return 0;

         foreach (var category in categories)
         {
            double weightDecimal = category.Weight / 100.0;
            totalCategoryWeight += weightDecimal;

            var categoryAssessments = targetAssessments.Where(a => a.Category == category.Name).ToList();
            double catEarned = 0;
            double catMax = 0;

            foreach (var assessment in categoryAssessments)
            {
               var score = studentScores.FirstOrDefault(s => s.AssessmentID == assessment.AssessmentID);
               if (score != null)
               {
                  catEarned += score.PointsEarned;
               }
               catMax += assessment.MaxScore;
            }

            if (catMax > 0)
            {
               hasAnyAssessments = true;
               double catPercentage = (catEarned / catMax) * 100.0;
               totalWeightedScore += (catPercentage * weightDecimal);
            }
         }

         if (hasAnyAssessments && totalCategoryWeight > 0)
         {
               academicGrade = totalWeightedScore / totalCategoryWeight;
         }

         return academicGrade;
      }

      // Applies attendance modifiers and transmutations (NRFG/CRG) to determine the final UI output and pass/fail status.

      public static GradeResult EvaluateFinalGrade(
         double rawAcademicGrade,
         TeacherClass teacherClass,
         GradingTemplate template,
         IEnumerable<GradeBoundary> boundaries,
         int totalActiveDays,
         double effectiveAbsences)
      {
         var result = new GradeResult { RawAcademicGrade = rawAcademicGrade };
         double finalNumeric = rawAcademicGrade;
         double passingScore = template?.PassingGrade ?? 75.0;

         // === 1. ATTENDANCE MODIFIERS ===
         string attMode = teacherClass.AttendanceCalculationMode ?? "None";
         
         switch (attMode)
         {
            case "Threshold":
               if (effectiveAbsences >= teacherClass.MaxAbsencesAllowed) 
               {
                  result.IsFA = true;
                  finalNumeric = -1;
               }
               break;

            case "Weighted":
               double attendanceScore = 100.0;
               if (totalActiveDays > 0)
               {
                  attendanceScore = ((totalActiveDays - effectiveAbsences) / totalActiveDays) * 100.0;
                  if (attendanceScore < 0) attendanceScore = 0;
               }
               double academicWeight = (100.0 - teacherClass.AttendanceWeight) / 100.0;
               double attWeight = teacherClass.AttendanceWeight / 100.0;
               finalNumeric = (rawAcademicGrade * academicWeight) + (attendanceScore * attWeight);
               break;

            case "Bonus":
               if (effectiveAbsences == 0 && totalActiveDays > 0) finalNumeric += teacherClass.AttendanceWeight;
               else if (effectiveAbsences > teacherClass.MaxAbsencesAllowed) finalNumeric -= teacherClass.AttendanceWeight;
               finalNumeric = Math.Clamp(finalNumeric, 0, 100);
               break;
         }

         // === 2. TRANSMUTATION (NRFG / CRG) ===
         if (result.IsFA || finalNumeric == -1)
         {
            result.FinalNumeric = -1;
            result.FinalOutput = "FA";
            result.IsFailing = true;
            return result;
         }

         string calcMode = template?.CalculationMode ?? "NRFG";
         
         if (calcMode == "NRFG")
         {
            double baseVal = template?.NrfgBaseValue ?? 60.0;
            finalNumeric = (finalNumeric / 100.0) * (100.0 - baseVal) + baseVal;
            result.FinalOutput = $"{finalNumeric.ToString("0.##")}%";
         }
         else if (calcMode == "CRG")
         {
            if (boundaries != null && boundaries.Any())
            {
               var matchingBand = boundaries.FirstOrDefault(b => finalNumeric >= b.MinScore && finalNumeric <= b.MaxScore);
               if (matchingBand != null)
               {
                  result.FinalOutput = matchingBand.Label ?? "";
                  finalNumeric = matchingBand.GpaValue;
               }
               else
               {
                  result.FinalOutput = $"{finalNumeric.ToString("0.##")}%";
               }
            }
            else
            {
               result.FinalOutput = $"{finalNumeric.ToString("0.##")}%";
            }
         }
         else
         {
            result.FinalOutput = $"{finalNumeric.ToString("0.##")}%";
         }

         // === 3. FINAL EVALUATION ===
         result.FinalNumeric = finalNumeric;
         result.IsFailing = finalNumeric < passingScore;

         return result;
      }
   }
}