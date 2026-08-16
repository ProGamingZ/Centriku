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

      public static GradeResult EvaluateFinalGrade(
         double rawAcademicGrade,
         GradingTemplate template)
      {
         var result = new GradeResult { RawAcademicGrade = rawAcademicGrade };
         double finalNumeric = rawAcademicGrade;
         double passingScore = template?.PassingGrade ?? 75.0;

         // 1. Always apply the Base-Value Transmutation!
         double baseVal = template?.NrfgBaseValue ?? 50.0;
         finalNumeric = (finalNumeric / 100.0) * (100.0 - baseVal) + baseVal;
         
         result.FinalOutput = $"{finalNumeric.ToString("0.##")}%";

         // 2. Final Evaluation
         result.FinalNumeric = finalNumeric;
         result.IsFailing = finalNumeric < passingScore;

         return result;
      }
   }
}