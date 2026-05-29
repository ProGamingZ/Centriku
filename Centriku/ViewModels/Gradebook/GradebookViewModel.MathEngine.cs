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
      private double CalculateAcademicGrade(StudentGradeRow row, string targetPeriod)
      {
         double academicGrade = 100.0; 
         double totalWeightedScore = 0;
         double totalActiveWeight = 0;

         if (AvailableCategories != null && ClassAssessments != null)
         {
            foreach (var category in AvailableCategories)
            {
               double catEarned = 0;
               double catMax = 0;

               // ONLY grab assessments that belong to this category AND this specific Grading Period
               var categoryAssessments = ClassAssessments.Where(a => a.Category == category.Name && a.GradingPeriod == targetPeriod).ToList();

               foreach (var assessment in categoryAssessments)
               {
                     if (row.Scores.TryGetValue(assessment.AssessmentID, out var cell))
                     {
                        catEarned += cell.PointsEarned;
                        catMax += assessment.MaxScore;
                     }
               }

               if (catMax > 0)
               {
                     double catPercentage = (catEarned / catMax) * 100.0;
                     double weightDecimal = category.Weight / 100.0;

                     totalWeightedScore += (catPercentage * weightDecimal);
                     totalActiveWeight += weightDecimal; 
               }
            }

            if (totalActiveWeight > 0) academicGrade = totalWeightedScore / totalActiveWeight;
         }
         return academicGrade;
      }
      public void RecalculateFinalGrades()
      {
         if (GradebookRows == null || AttendanceGridRows == null) return;

         foreach (var row in GradebookRows)
         {
            // === 1. TERM AVERAGING MATH ===
            double finalAcademicGrade = 100.0;

            if (SelectedTermView == "Semester Average")
            {
               double midterm = CalculateAcademicGrade(row, "Midterm");
               double final = CalculateAcademicGrade(row, "Final");

               row.MidtermGradeDisplay = $"{System.Math.Round(midterm, 2)}%";
               row.FinalTermGradeDisplay = $"{System.Math.Round(final, 2)}%";
               row.MidtermGradeNumeric = midterm;
               row.FinalTermGradeNumeric = final;
               // Safety Check: Prevent 0% averages if the teacher hasn't created a Final yet!
               bool hasMidterm = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Midterm");
               bool hasFinal = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Final");
               
               if (hasMidterm && hasFinal) finalAcademicGrade = (midterm + final) / 2.0;
               else if (hasMidterm) finalAcademicGrade = midterm;
               else if (hasFinal) finalAcademicGrade = final;
            }
            else
            {
               finalAcademicGrade = CalculateAcademicGrade(row, SelectedTermView);
            }

            // === 2. ATTENDANCE PENALTIES ===
            var attendanceRow = AttendanceGridRows.FirstOrDefault(a => a.StudentInfo.StudentID == row.StudentID);
            int totalDays = attendanceRow?.Cells.Count ?? 0;
            int excusedDays = attendanceRow?.TotalE ?? 0;
            int activeDays = totalDays - excusedDays;
            double effectiveAbsences = (attendanceRow?.TotalA ?? 0) + ((attendanceRow?.TotalL ?? 0) * LateValue);

            string finalOutput = "";
            double finalNumeric = 0;
            switch (AttendanceCalculationMode)
            {
               case "None":
                     finalOutput = $"{System.Math.Round(finalAcademicGrade, 2)}%";
                     finalNumeric = finalAcademicGrade;
                     break;

               case "Threshold":
                     if (effectiveAbsences >= MaxAbsencesAllowed)
                     {
                        finalOutput = "FA";
                        finalNumeric = -1; 
                     }
                     else
                     {
                        finalOutput = $"{System.Math.Round(finalAcademicGrade, 2)}%";
                        finalNumeric = finalAcademicGrade;
                     }
                     break;

               case "Weighted":
                     double attendanceScore = 100.0;
                     if (activeDays > 0)
                     {
                        attendanceScore = ((activeDays - effectiveAbsences) / activeDays) * 100.0;
                        if (attendanceScore < 0) attendanceScore = 0;
                     }

                     double academicWeight = (100.0 - AttendanceWeight) / 100.0;
                     double attWeight = AttendanceWeight / 100.0;
                     
                     double weightedFinal = (finalAcademicGrade * academicWeight) + (attendanceScore * attWeight);
                     
                     finalOutput = $"{System.Math.Round(weightedFinal, 2)}%";
                     finalNumeric = weightedFinal;
                     break;

               case "Bonus":
                     double bonusFinal = finalAcademicGrade;
                     if (effectiveAbsences == 0 && activeDays > 0) 
                        bonusFinal += AttendanceWeight; 
                     else if (effectiveAbsences > MaxAbsencesAllowed)
                        bonusFinal -= AttendanceWeight; 
                     
                     if (bonusFinal > 100) bonusFinal = 100;
                     if (bonusFinal < 0) bonusFinal = 0;

                     finalOutput = $"{System.Math.Round(bonusFinal, 2)}%";
                     finalNumeric = bonusFinal;
                     break;
            }

            switch (CalculationMode)
            {
               case "NRFG":
                     // THE RUBBER BAND (Norm-Referenced Formula) 
                     // Universal Formula: Transmuted = (Raw / 100) * (100 - Base) + Base
                     double baseVal = NrfgBaseValue;
                     finalNumeric = (finalNumeric / 100.0) * (100.0 - baseVal) + baseVal;
                     
                     // We round to 0 decimal places for NRFG curves usually
                     finalOutput = $"{System.Math.Round(finalNumeric, 0)}"; 
                     break;

               case "CRG":
                     // THE SORTING BUCKETS (Criterion-Referenced Boundaries) 
                     if (ClassGradeBoundaries != null && ClassGradeBoundaries.Any())
                     {
                        var matchingBand = ClassGradeBoundaries.FirstOrDefault(b => finalNumeric >= b.MinScore && finalNumeric <= b.MaxScore);
                        if (matchingBand != null)
                        {
                           finalOutput = matchingBand.Label ?? "";
                           finalNumeric = matchingBand.GpaValue; 
                        }
                     }
                     break;

               default:
                     finalOutput = $"{System.Math.Round(finalNumeric, 2)}%";
                     break;
            }

            // 4. Lock it into the UI!
            row.FinalGrade = finalOutput;
            row.FinalGradeNumeric = finalNumeric;
         }
      }            

   }
}