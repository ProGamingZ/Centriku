using System;
using System.Collections.Generic;
using System.Linq;
using Centriku.Models;

namespace Centriku.Services
{
   public class GradeResult
   {
      public double TermNumericGrade { get; set; }
      public string FinalOutput { get; set; } = string.Empty;
      public bool IsFailing { get; set; }
   }

   public static class GradeCalculationService
   {
      public static GradeResult EvaluateFinalGrade(
         IEnumerable<GradingCategory> categories,
         IEnumerable<Assessment> termAssessments,
         IEnumerable<Score> studentScores,
         GradingTemplate template)
      {
         var result = new GradeResult();
         
         if (categories == null || !categories.Any() || !termAssessments.Any())
         {
             result.FinalOutput = "--";
             return result;
         }

         double totalExcelWS = 0;
         double baseVal = template?.NrfgBaseValue ?? 50.0;
         double passingScore = template?.PassingGrade ?? 75.0;
         bool isMissingCategory = false;

         foreach (var category in categories)
         {
            double weightDec = category.Weight / 100.0;
            
            var catAssessments = termAssessments
               .Where(a => string.Equals((a.Category ?? string.Empty).Trim(), (category.Name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
               .ToList();

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
               // Evaluate TS and WS per category (Matches Gradebook MathEngine)
               double ts = Math.Round((earned / max) * (100.0 - baseVal) + baseVal, 2, MidpointRounding.AwayFromZero);
               double ws = Math.Round(ts * weightDec, 2, MidpointRounding.AwayFromZero);
               totalExcelWS += ws;
            }
            else
            {
               isMissingCategory = true; 
            }
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