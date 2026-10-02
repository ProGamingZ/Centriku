using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.ViewModels.Modals;
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
      }
      private async Task SaveAssessmentAsync()
      {
         try 
         {
             if (string.IsNullOrWhiteSpace(NewAssessmentTitle) || SelectedCategory == null || NewAssessmentMaxScore <= 0) 
                return;

             // EXCEL LIMIT VALIDATION ---
             int existingCount = ClassAssessments.Count(a => MatchesCategory(a.Category, SelectedCategory.Name) && MatchesGradingPeriod(a.GradingPeriod, NewAssessmentPeriod));
             
             if (_editingAssessmentId.HasValue) 
             {
                existingCount = ClassAssessments.Count(a => MatchesCategory(a.Category, SelectedCategory.Name) && MatchesGradingPeriod(a.GradingPeriod, NewAssessmentPeriod) && a.AssessmentID != _editingAssessmentId.Value);
             }

             int maxAllowed = SelectedCategory.SequenceOrder switch { 1 => 10, 2 => 5, 3 => 1, _ => 10 };

             if (existingCount >= maxAllowed)
             {
                ShowToastMessage?.Invoke($"Limit Reached: Official class records only allow {maxAllowed} assessment(s) for {SelectedCategory.Name} per term.");
                return;
             }

             var db = new DatabaseService().GetConnection();
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
                assessmentToUpdate.GroupWeight = 0;
                assessmentToUpdate.IndividualWeight = 0;

                await db.UpdateAsync(assessmentToUpdate);
             }
             else
             {
                // === CREATE MODE ===
                var newAssessment = new Assessment
                {
                   ClassID = ClassId, Title = NewAssessmentTitle, Category = SelectedCategory.Name, GradingPeriod = NewAssessmentPeriod,
                   MaxScore = NewAssessmentMaxScore, DateGiven = NewAssessmentDate ?? System.DateTime.Now, AssessmentType = NewAssessmentType,
                   GroupWeight = 0, IndividualWeight = 0
                };
                await db.InsertAsync(newAssessment);
             }
             ResetAssessmentForm();
             await LoadGradebookData(); 
             await LoadGroupsDataAsync(); 
         }
         catch (Exception ex)
         {
             ShowToastMessage?.Invoke($"Error saving assessment: {ex.Message}");
         }
      }
      [ObservableProperty] public partial bool IsDeleteAssessmentModalOpen { get; set; } = false;
      [ObservableProperty] public partial ConfirmDeleteModalViewModel? DeleteAssessmentModal { get; set; }

      // 1. Opens the confirmation modal
      private void DeleteAssessment(Assessment? assessmentToDel)
      {
         if (assessmentToDel == null) return;
         DeleteAssessmentModal = new ConfirmDeleteModalViewModel(
            "Delete Assessment Column?",
            $"Are you sure you want to delete '{assessmentToDel.Title}'?\n\nThis will permanently erase all student scores and group configurations attached to this column.",
            async () =>
            {
               var db = new DatabaseService().GetConnection();
               
               // 1. Delete associated scores & group data
               var scores = await db.Table<Score>().Where(s => s.AssessmentID == assessmentToDel.AssessmentID).ToListAsync();
               var groups = await db.Table<AssessmentGroup>().Where(g => g.AssessmentID == assessmentToDel.AssessmentID).ToListAsync();
               var members = await db.Table<AssessmentGroupMember>().Where(m => m.AssessmentID == assessmentToDel.AssessmentID).ToListAsync();
               
               await db.RunInTransactionAsync(tran =>
               {
                  foreach (var s in scores) tran.Delete(s);
                  foreach (var g in groups) tran.Delete(g);
                  foreach (var m in members) tran.Delete(m);
                  tran.Delete(assessmentToDel); // Finally, delete the column itself
               });

               await LoadGradebookData();
               ShowToastMessage?.Invoke($"Successfully deleted '{assessmentToDel.Title}'.");
            },
            () => { IsDeleteAssessmentModalOpen = false; DeleteAssessmentModal = null; }
         );
         IsDeleteAssessmentModalOpen = true;
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
      }

      [ObservableProperty] public partial ObservableCollection<string> AssessmentTypeOptions { get; set; } = new() { "Solo", "Group/Pair" };
      [ObservableProperty] public partial string NewAssessmentType { get; set; } = "Solo";
      public bool IsGroupAssessmentSelected => NewAssessmentType == "Group/Pair";

      partial void OnNewAssessmentTypeChanged(string value)
      {
          OnPropertyChanged(nameof(IsGroupAssessmentSelected));
      }
   }
}