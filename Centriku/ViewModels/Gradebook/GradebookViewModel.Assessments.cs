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
      [ObservableProperty] public partial bool IsAddingAssessment { get; set; } = false;
      [ObservableProperty] public partial string NewAssessmentTitle { get; set; } = string.Empty;
      [ObservableProperty] public partial double NewAssessmentMaxScore { get; set; } = 100;
      [ObservableProperty] public partial System.DateTime? NewAssessmentDate { get; set; } = System.DateTime.Now;
      [ObservableProperty] public partial string NewAssessmentPeriod { get; set; } = "Midterm"; 
      private int? _editingAssessmentId = null;
      public IRelayCommand ToggleAddAssessmentCommand { get; }
      public IRelayCommand SaveAssessmentCommand { get; }
      public IRelayCommand<Assessment> EditAssessmentCommand { get; }
      public IRelayCommand<Assessment> DeleteAssessmentCommand { get; }

      private void EditAssessment(Assessment assessment)
      {
         if (assessment == null) return;
         _editingAssessmentId = assessment.AssessmentID;
         NewAssessmentTitle = assessment.Title ?? string.Empty;
         NewAssessmentMaxScore = assessment.MaxScore;
         NewAssessmentDate = assessment.DateGiven;
         SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Name == assessment.Category);
         NewAssessmentPeriod = assessment.GradingPeriod ?? "Midterm"; // <== ADD THIS
         IsAddingAssessment = true; 
      }
      private async void SaveAssessment()
      {
         if (string.IsNullOrWhiteSpace(NewAssessmentTitle) || SelectedCategory == null || NewAssessmentMaxScore <= 0) 
            return;

         var db = new DatabaseService().GetConnection();

         if (_editingAssessmentId.HasValue)
         {
            // === UPDATE MODE ===
            var assessmentToUpdate = await db.Table<Assessment>().Where(a => a.AssessmentID == _editingAssessmentId.Value).FirstOrDefaultAsync();
            assessmentToUpdate.Title = NewAssessmentTitle;
            assessmentToUpdate.Category = SelectedCategory.Name;
            
            // === FIX: Tell SQLite which term this belongs to! ===
            assessmentToUpdate.GradingPeriod = NewAssessmentPeriod; 
            // ====================================================

            assessmentToUpdate.MaxScore = NewAssessmentMaxScore;
            assessmentToUpdate.DateGiven = NewAssessmentDate ?? System.DateTime.Now;
            
            await db.UpdateAsync(assessmentToUpdate);
         }
         else
         {
            // === CREATE MODE ===
            var newAssessment = new Assessment
            {
               ClassID = ClassId,
               Title = NewAssessmentTitle,
               Category = SelectedCategory.Name,
               
               // === FIX: Save the dropdown selection to SQLite! ===
               GradingPeriod = NewAssessmentPeriod,
               // ===================================================

               MaxScore = NewAssessmentMaxScore,
               DateGiven = NewAssessmentDate ?? System.DateTime.Now
            };
            await db.InsertAsync(newAssessment);
         }
         ResetAssessmentForm();
         await LoadGradebookData(); // Refresh the grid!
      }
      private async void DeleteAssessment(Assessment assessment)
      {
         if (assessment == null) return;
         var db = new DatabaseService().GetConnection();
         
         // 1. Delete the Assessment column (Table 4)
         await db.DeleteAsync(assessment);

         // 2. Wipe all the student scores associated with this exact Quiz (Table 5)
         var scoresToDelete = await db.Table<Score>().Where(s => s.AssessmentID == assessment.AssessmentID).ToListAsync();
         foreach (var score in scoresToDelete)
         {
            await db.DeleteAsync(score);
         }

         await LoadGradebookData(); // Refresh the grid
      }
      private void ResetAssessmentForm()
      {
         _editingAssessmentId = null; // Clears the "Edit Mode" tracking ID
         NewAssessmentTitle = string.Empty;
         NewAssessmentMaxScore = 100;
         NewAssessmentDate = System.DateTime.Now;
         SelectedCategory = null;
         NewAssessmentPeriod = IsSemesterAverageView ? (GradingPeriods.FirstOrDefault() ?? "Midterm") : SelectedTermView;
         IsAddingAssessment = false; // Hides the form
      }

   }
}