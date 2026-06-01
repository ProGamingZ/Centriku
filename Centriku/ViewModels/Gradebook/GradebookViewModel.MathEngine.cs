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
      public void RecalculateFinalGrades()
      {
         if (GradebookRows == null || AttendanceGridRows == null) return;

         foreach (var row in GradebookRows)
         {
            // 1. HYBRID TERM AVERAGING MATH 
            double finalAcademicGrade = 100.0;
            bool isFinalMathComplete = false; 

            // Check if the user is viewing the "Overall Average" column
            if (SelectedTermView == "Semester Average" || SelectedTermView == "Final Average")
            {
               if (EducationMode == "Semestral")
               {
                  double midterm = CalculateAcademicGrade(row, "Midterm");
                  double final = CalculateAcademicGrade(row, "Final");

                  bool hasMidterm = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Midterm");
                  bool hasFinal = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Final");

                  // UPGRADED FORMATTING: .ToString("0.##")
                  row.MidtermGradeDisplay = hasMidterm ? $"{midterm.ToString("0.##")}%" : "--";
                  row.FinalTermGradeDisplay = hasFinal ? $"{final.ToString("0.##")}%" : "--";
                  row.MidtermGradeNumeric = midterm;
                  row.FinalTermGradeNumeric = final;

                  if (hasMidterm && hasFinal)
                  {
                     finalAcademicGrade = (midterm + final) / 2.0;
                     isFinalMathComplete = true;
                  }
               }
               else // "Quarterly" Mode
               {
                  double q1 = CalculateAcademicGrade(row, "Q1");
                  double q2 = CalculateAcademicGrade(row, "Q2");
                  double q3 = CalculateAcademicGrade(row, "Q3");
                  double q4 = CalculateAcademicGrade(row, "Q4");

                  bool hasQ1 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q1");
                  bool hasQ2 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q2");
                  bool hasQ3 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q3");
                  bool hasQ4 = ClassAssessments != null && ClassAssessments.Any(a => a.GradingPeriod == "Q4");

                  // UPGRADED FORMATTING: .ToString("0.##")
                  row.Q1GradeDisplay = hasQ1 ? $"{q1.ToString("0.##")}%" : "--";
                  row.Q2GradeDisplay = hasQ2 ? $"{q2.ToString("0.##")}%" : "--";
                  row.Q3GradeDisplay = hasQ3 ? $"{q3.ToString("0.##")}%" : "--";
                  row.Q4GradeDisplay = hasQ4 ? $"{q4.ToString("0.##")}%" : "--";
                  
                  row.Q1GradeNumeric = q1;
                  row.Q2GradeNumeric = q2;
                  row.Q3GradeNumeric = q3;
                  row.Q4GradeNumeric = q4;

                  if (hasQ1 && hasQ2 && hasQ3 && hasQ4)
                  {
                     finalAcademicGrade = (q1 + q2 + q3 + q4) / 4.0;
                     isFinalMathComplete = true;
                  }
               }
            }
            else
            {
               // If viewing just a specific single term (e.g., viewing only Q1), the math is always "complete"
               finalAcademicGrade = CalculateAcademicGrade(row, SelectedTermView);
               isFinalMathComplete = true;
            }

            // === 2. ATTENDANCE PENALTIES ===
            var attendanceRow = AttendanceGridRows.FirstOrDefault(a => a.StudentInfo.StudentID == row.StudentID);
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

                // 3. NRFG / CRG TRANSMUTATION 
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