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
         double academicGrade = 0; // Default to 0 instead of 100 if no tasks exist
         double totalWeightedScore = 0;
         double totalCategoryWeight = 0; 
         bool hasAnyAssessments = false;

         if (AvailableCategories != null && ClassAssessments != null)
         {
            foreach (var category in AvailableCategories)
            {
               // ALWAYS add the category's weight to the denominator
               double weightDecimal = category.Weight / 100.0;
               totalCategoryWeight += weightDecimal; 

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
                     hasAnyAssessments = true;
                     double catPercentage = (catEarned / catMax) * 100.0;
                     totalWeightedScore += (catPercentage * weightDecimal);
               }
               // If catMax is 0, nothing is added to totalWeightedScore, acting as a strict 0.
            }
            // Divide by the total weight of ALL categories (e.g., 1.0) rather than just the active ones
            if (hasAnyAssessments && totalCategoryWeight > 0) 
            {
                academicGrade = totalWeightedScore / totalCategoryWeight;
            }
         }
         return academicGrade;
      }
      
      private string FormatSummaryGrade(double rawGrade, bool hasAssessments)
      {
         if (!hasAssessments) return "--";

         if (CalculationMode == "NRFG")
         {
               double transmuted = (rawGrade / 100.0) * (100.0 - NrfgBaseValue) + NrfgBaseValue;
               return $"{transmuted.ToString("0.##")}%";
         }
         else if (CalculationMode == "CRG")
         {
               if (ClassGradeBoundaries != null && ClassGradeBoundaries.Count != 0)
               {
                  var matchingBand = ClassGradeBoundaries.FirstOrDefault(b => rawGrade >= b.MinScore && rawGrade <= b.MaxScore);
                  if (matchingBand != null) return matchingBand.Label ?? "";
               }
         }
         
         return $"{rawGrade.ToString("0.##")}%";
      }
     
      public void RecalculateFinalGrades()
      {
         RecalculateFinalGradesForList(GradebookRows, AttendanceGridRows);
      }

      public void RecalculateFinalGradesForList(System.Collections.Generic.IEnumerable<StudentGradeRow> targetGradeRows, System.Collections.Generic.IEnumerable<AttendanceGridRowViewModel> targetAttRows)
      {
         if (targetGradeRows == null || targetAttRows == null) return;

         foreach (var row in targetGradeRows)
         {
            // === 1. ALWAYS CALCULATE EVERY TERM ===
            bool hasMidterm = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Midterm");
            double midterm = CalculateAcademicGrade(row, "Midterm");
            row.MidtermGradeDisplay = FormatSummaryGrade(midterm, hasMidterm);
            row.MidtermGradeNumeric = midterm;

            bool hasFinal = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Final");
            double final = CalculateAcademicGrade(row, "Final");
            row.FinalTermGradeDisplay = FormatSummaryGrade(final, hasFinal);
            row.FinalTermGradeNumeric = final;

            bool hasQ1 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q1");
            double q1 = CalculateAcademicGrade(row, "Q1");
            row.Q1GradeDisplay = FormatSummaryGrade(q1, hasQ1);
            row.Q1GradeNumeric = q1;

            bool hasQ2 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q2");
            double q2 = CalculateAcademicGrade(row, "Q2");
            row.Q2GradeDisplay = FormatSummaryGrade(q2, hasQ2);
            row.Q2GradeNumeric = q2;

            bool hasQ3 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q3");
            double q3 = CalculateAcademicGrade(row, "Q3");
            row.Q3GradeDisplay = FormatSummaryGrade(q3, hasQ3);
            row.Q3GradeNumeric = q3;

            bool hasQ4 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q4");
            double q4 = CalculateAcademicGrade(row, "Q4");
            row.Q4GradeDisplay = FormatSummaryGrade(q4, hasQ4);
            row.Q4GradeNumeric = q4;

            // === 2. DETERMINE THE 'ACTIVE' FINAL GRADE FOR THE UI ===
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
               // If viewing a single term, grab its pre-calculated value!
               finalAcademicGrade = SelectedTermView switch {
                   "Q1" => q1, "Q2" => q2, "Q3" => q3, "Q4" => q4, "Midterm" => midterm, "Final" => final, _ => 100.0
               };
               isFinalMathComplete = SelectedTermView switch {
                   "Q1" => hasQ1, "Q2" => hasQ2, "Q3" => hasQ3, "Q4" => hasQ4, "Midterm" => hasMidterm, "Final" => hasFinal, _ => false
               };
            }

            // === 3. ATTENDANCE PENALTIES ===
            var attendanceRow = targetAttRows.FirstOrDefault(a => a.StudentInfo.StudentID == row.StudentID);
            int totalDays = attendanceRow?.Cells.Count ?? 0;
            int excusedDays = attendanceRow?.TotalE ?? 0;
            int activeDays = totalDays - excusedDays;
            double effectiveAbsences = (attendanceRow?.TotalA ?? 0) + ((attendanceRow?.TotalL ?? 0) * LateValue);

            double finalNumeric = 0;
            string finalOutput = "";

            if (!isFinalMathComplete)
            {
                // FORCE THE INCOMPLETE SYMBOL
                finalNumeric = 0;
                finalOutput = "--";
            }
            else
            {
                // Process standard math if the term/year is complete
                switch (AttendanceCalculationMode)
                {
                   case "None":
                         finalNumeric = finalAcademicGrade;
                         break;
                   case "Threshold":
                         if (effectiveAbsences >= MaxAbsencesAllowed) finalNumeric = -1; 
                         else finalNumeric = finalAcademicGrade;
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
                         finalNumeric = (finalAcademicGrade * academicWeight) + (attendanceScore * attWeight);
                         break;
                   case "Bonus":
                         double bonusFinal = finalAcademicGrade;
                         if (effectiveAbsences == 0 && activeDays > 0) bonusFinal += AttendanceWeight; 
                         else if (effectiveAbsences > MaxAbsencesAllowed) bonusFinal -= AttendanceWeight; 
                         
                         if (bonusFinal > 100) bonusFinal = 100;
                         if (bonusFinal < 0) bonusFinal = 0;
                         finalNumeric = bonusFinal;
                         break;
                }

                // NRFG / CRG TRANSMUTATION 
                if (finalNumeric == -1)
                {
                    finalOutput = "FA"; // Failure due to Absences
                }
                else
                {
                    switch (CalculationMode)
                    {
                       case "NRFG":
                             double baseVal = NrfgBaseValue;
                             finalNumeric = (finalNumeric / 100.0) * (100.0 - baseVal) + baseVal;
                             // NRFG retains decimals!
                             finalOutput = $"{finalNumeric.ToString("0.##")}%"; 
                             break;
                       case "CRG":
                             if (ClassGradeBoundaries != null && ClassGradeBoundaries.Count != 0)
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
                             // ToString("0.##")
                             finalOutput = $"{finalNumeric.ToString("0.##")}%";
                             break;
                    }
                }
            }

            // 4. Lock it into the UI!
            row.FinalGrade = finalOutput;
            row.FinalGradeNumeric = finalNumeric;
         }
      }
   
   }
}