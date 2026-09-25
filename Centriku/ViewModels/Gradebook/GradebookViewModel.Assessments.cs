using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;
using System;

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
         SelectedCategory = AvailableCategories.FirstOrDefault(c => MatchesCategory(c.Name, assessment.Category));
         NewAssessmentPeriod = string.IsNullOrWhiteSpace(assessment.GradingPeriod) ? "Midterm" : assessment.GradingPeriod.Trim();
         IsAddingAssessment = true;
         IsEnrolling = false; 
         NewAssessmentType = string.IsNullOrEmpty(assessment.AssessmentType) ? "Solo" : assessment.AssessmentType;
         NewAssessmentGroupWeight = assessment.GroupWeight > 0 ? assessment.GroupWeight : 30;
         NewAssessmentIndividualWeight = assessment.IndividualWeight > 0 ? assessment.IndividualWeight : 70;
      }
      private async void SaveAssessment()
      {
         if (string.IsNullOrWhiteSpace(NewAssessmentTitle) || SelectedCategory == null || NewAssessmentMaxScore <= 0) 
            return;

         if (NewAssessmentType == "Group/Pair")
         {
             if (Math.Round(NewAssessmentGroupWeight + NewAssessmentIndividualWeight, 2) != 100.0)
             {
                 ShowToastMessage?.Invoke("Group Weight and Individual Weight must total exactly 100%.");
                 return;
             }
         }

         // EXCEL LIMIT VALIDATION ---
         int existingCount = ClassAssessments.Count(a => MatchesCategory(a.Category, SelectedCategory.Name) && MatchesGradingPeriod(a.GradingPeriod, NewAssessmentPeriod));
         
         // If editing, don't count the current assessment against the limit
         if (_editingAssessmentId.HasValue) 
         {
            existingCount = ClassAssessments.Count(a => MatchesCategory(a.Category, SelectedCategory.Name) && MatchesGradingPeriod(a.GradingPeriod, NewAssessmentPeriod) && a.AssessmentID != _editingAssessmentId.Value);
         }

         int maxAllowed = SelectedCategory.SequenceOrder switch {
            1 => 10, // Class Standing limit
            2 => 5,  // MCO limit
            3 => 1,  // Major Exam limit
            _ => 10
         };

         if (existingCount >= maxAllowed)
         {
            ShowToastMessage?.Invoke($"Limit Reached: Official class records only allow {maxAllowed} assessment(s) for {SelectedCategory.Name} per term.");
            return;
         }
         // -----------------------------------

         var db = new DatabaseService().GetConnection();
         //Forces SQLite to scan the model and append the missing Group/Solo columns to your existing database!
         await db.CreateTableAsync<Assessment>();
         if (_editingAssessmentId.HasValue)
         {
            // === UPDATE MODE ===
            var assessmentToUpdate = await db.Table<Assessment>().Where(a => a.AssessmentID == _editingAssessmentId.Value).FirstOrDefaultAsync();
            assessmentToUpdate.Title = NewAssessmentTitle;
            assessmentToUpdate.Category = SelectedCategory.Name;
            assessmentToUpdate.GradingPeriod = NewAssessmentPeriod; 
            assessmentToUpdate.MaxScore = NewAssessmentMaxScore;
            assessmentToUpdate.DateGiven = NewAssessmentDate ?? System.DateTime.Now;
            assessmentToUpdate.AssessmentType = NewAssessmentType;
            assessmentToUpdate.GroupWeight = NewAssessmentGroupWeight;
            assessmentToUpdate.IndividualWeight = NewAssessmentIndividualWeight;

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
               GradingPeriod = NewAssessmentPeriod,
               MaxScore = NewAssessmentMaxScore,
               DateGiven = NewAssessmentDate ?? System.DateTime.Now,
               AssessmentType = NewAssessmentType,
               GroupWeight = NewAssessmentGroupWeight,
               IndividualWeight = NewAssessmentIndividualWeight
            };
            await db.InsertAsync(newAssessment);

         }
         ResetAssessmentForm();
         await LoadGradebookData(); // Refresh the grid!
         await LoadGroupsDataAsync(); // Refreshes the Groups Tab Dropdown!
      }
      [ObservableProperty] public partial bool IsDeleteAssessmentModalOpen { get; set; } = false;
      [ObservableProperty] public partial string DeleteAssessmentMessage { get; set; } = string.Empty;
      private Assessment? _assessmentToDelete;

      // 1. Opens the confirmation modal
      private void DeleteAssessment(Assessment assessment)
      {
         if (assessment == null) return;
         _assessmentToDelete = assessment;
         DeleteAssessmentMessage = $"Are you sure you want to delete '{assessment.Title}'?\n\nThis will permanently erase all student scores and group data tied to this column.";
         IsDeleteAssessmentModalOpen = true;
      }

      // 2. The Bulk Deletion Engine
      [RelayCommand]
      public async Task ConfirmDeleteAssessment()
      {
         if (_assessmentToDelete == null) return;
         IsProcessing = true; // Turn on the loading spinner!
         
         try
         {
               var db = new DatabaseService().GetConnection();
               int id = _assessmentToDelete.AssessmentID;

               // 1. Safely fetch all related records using the ORM (No raw SQL strings!)
               var scoresToDelete = await db.Table<Score>().Where(s => s.AssessmentID == id).ToListAsync();
               var membersToDelete = await db.Table<AssessmentGroupMember>().Where(m => m.AssessmentID == id).ToListAsync();
               var groupsToDelete = await db.Table<AssessmentGroup>().Where(g => g.AssessmentID == id).ToListAsync();

               // 2. Perform a single Bulk Transaction. This locks the database once, deletes all 50+ items instantly, and unlocks.
               await db.RunInTransactionAsync(tran => 
               {
                   foreach (var s in scoresToDelete) tran.Delete(s);
                   foreach (var m in membersToDelete) tran.Delete(m);
                   foreach (var g in groupsToDelete) tran.Delete(g);
                   
                   // Finally, delete the Assessment column itself
                   tran.Delete(_assessmentToDelete); 
               });

               await LoadGradebookData();
               ShowToastMessage?.Invoke($"Deleted assessment: '{_assessmentToDelete.Title}'.");
               CancelDeleteAssessment();
         }
         catch (Exception ex)
         {
               // If anything fails, gently show a toast instead of crashing the app!
               ShowToastMessage?.Invoke($"Error deleting assessment: {ex.Message}");
         }
         finally { IsProcessing = false; }
      }

      [RelayCommand]
      public void CancelDeleteAssessment()
      {
         IsDeleteAssessmentModalOpen = false;
         _assessmentToDelete = null;
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
         NewAssessmentType = "Solo";
         NewAssessmentGroupWeight = 30;
         NewAssessmentIndividualWeight = 70;
      }

      [ObservableProperty] public partial ObservableCollection<string> AssessmentTypeOptions { get; set; } = new() { "Solo", "Group/Pair" };
      [ObservableProperty] public partial string NewAssessmentType { get; set; } = "Solo";
      [ObservableProperty] public partial double NewAssessmentGroupWeight { get; set; } = 30;
      [ObservableProperty] public partial double NewAssessmentIndividualWeight { get; set; } = 70;
      public bool IsGroupAssessmentSelected => NewAssessmentType == "Group/Pair";

      partial void OnNewAssessmentTypeChanged(string value)
      {
          OnPropertyChanged(nameof(IsGroupAssessmentSelected));
      }
   }
}