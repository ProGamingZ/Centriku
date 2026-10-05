using System;
using System.Collections.Generic;
using System.Linq;
using Centriku.Models;
using Centriku.ViewModels; 

namespace Centriku.Services
{
   public class GradeResult
   {
      public double TermNumericGrade { get; set; }
      public string FinalOutput { get; set; } = string.Empty;
      public bool IsFailing { get; set; }
      public bool IsFda { get; set; } = false; // Tracks if failure was due to absences
   }

   public static class GradeCalculationService
   {
      public static double CalculateTransmutedScore(double earned, double max, double baseVal)
      {
          if (max <= 0) return 0;
          return Math.Round((earned / max) * (100.0 - baseVal) + baseVal, 2, MidpointRounding.AwayFromZero);
      }

      public static GradeResult EvaluateFinalGrade(
         IEnumerable<GradingCategory> categories,
         IEnumerable<Assessment> termAssessments,
         IEnumerable<Score> studentScores,
         GradingTemplate template,
         TeacherClass currentClass,                          // The Rules
         IEnumerable<AttendanceRecord> attendanceRecords)    // The Raw Data
      {
         var result = new GradeResult();
         
         if (categories == null || !categories.Any() || !termAssessments.Any())
         {
             result.FinalOutput = "--";
             return result;
         }

         // --- FDA (Failure Due to Absences) OVERRIDE ---
         if (currentClass != null && currentClass.IsFdaEnabled && attendanceRecords != null)
         {
             bool dropStudent = false;
             
             // Calculate decimal hours directly from the raw data
             double totalA = attendanceRecords.Where(a => a.Status == "A").Sum(a => a.SessionHours);
             double totalL = attendanceRecords.Where(a => a.Status == "L").Sum(a => a.SessionHours);

             if (currentClass.FdaRuleType == "Equivalent")
             {
                 double equivalentAbsences = totalA + (totalL * currentClass.FdaLatePenaltyRatio);
                 if (equivalentAbsences >= currentClass.FdaMaxAbsenceHours) dropStudent = true;
             }
             else if (currentClass.FdaRuleType == "Independent")
             {
                 if (totalA >= currentClass.FdaMaxAbsenceHours || totalL >= currentClass.FdaMaxLateHours)
                 {
                     dropStudent = true;
                 }
             }

             if (dropStudent)
             {
                 result.TermNumericGrade = 0; 
                 result.FinalOutput = currentClass.FdaDropGrade ?? "FDA";
                 result.IsFailing = true;
                 result.IsFda = true; // Alerts the Dashboard that this is a critical drop
                 return result; 
             }
         }
         // ---------------------------------------------------

         double totalExcelWS = 0;
         double baseVal = template?.NrfgBaseValue ?? 50.0;
         double passingScore = template?.PassingGrade ?? 75.0;
         bool isMissingCategory = false;

         foreach (var category in categories)
         {
            double weightDec = category.Weight / 100.0;
            var catAssessments = termAssessments.Where(a => string.Equals((a.Category ?? string.Empty).Trim(), (category.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

            double earned = 0;
            double max = 0;

            foreach (var a in catAssessments)
            {
               var score = studentScores.FirstOrDefault(s => s.AssessmentID == a.AssessmentID);
               if (score != null && !score.IsExcused && a.MaxScore > 0)
               {
                  earned += score.PointsEarned;
                  max += a.MaxScore;
               }
            }

            if (max > 0)
            {
               double ts = CalculateTransmutedScore(earned, max, baseVal);
               double ws = Math.Round(ts * weightDec, 2, MidpointRounding.AwayFromZero);
               totalExcelWS += ws;
            }
            else { isMissingCategory = true; }
         }

         if (isMissingCategory)
         {
            result.FinalOutput = "--";
            return result;
         }

         double finalTermGrade = Math.Round(totalExcelWS, 0, MidpointRounding.AwayFromZero);
         result.TermNumericGrade = finalTermGrade;
         result.FinalOutput = $"{finalTermGrade}%";
         result.IsFailing = finalTermGrade < passingScore;

         return result;
      }
   }
}