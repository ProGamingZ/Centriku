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

            // EXCEL LIMIT VALIDATION (0-Based SequenceOrder: 0=10, 1=5, 2=1)
            int existingCount = ClassAssessments.Count(a => MatchesCategory(a.Category, SelectedCategory.Name) && MatchesGradingPeriod(a.GradingPeriod, NewAssessmentPeriod));
            
            if (_editingAssessmentId.HasValue) 
            {
               existingCount = ClassAssessments.Count(a => MatchesCategory(a.Category, SelectedCategory.Name) && MatchesGradingPeriod(a.GradingPeriod, NewAssessmentPeriod) && a.AssessmentID != _editingAssessmentId.Value);
            }

            int maxAllowed = SelectedCategory.SequenceOrder switch { 0 => 10, 1 => 5, 2 => 1, _ => 10 };

            if (existingCount >= maxAllowed)
            {
               ShowToastMessage?.Invoke($"Limit Reached: Official class records only allow {maxAllowed} assessment(s) for {SelectedCategory.Name} per term.");
               return;
            }

            var db = _dbService.GetConnection();
            await db.CreateTableAsync<Assessment>();

            if (_editingAssessmentId.HasValue)
            {
               // === UPDATE MODE ===
               var assessmentToUpdate = await db.Table<Assessment>().Where(a => a.AssessmentID == _editingAssessmentId.Value).FirstOrDefaultAsync();
               if (assessmentToUpdate != null)
               {
                  assessmentToUpdate.Title = NewAssessmentTitle.Trim();
                  assessmentToUpdate.Category = SelectedCategory.Name;
                  assessmentToUpdate.GradingPeriod = NewAssessmentPeriod; 
                  assessmentToUpdate.MaxScore = NewAssessmentMaxScore;
                  assessmentToUpdate.DateGiven = NewAssessmentDate ?? System.DateTime.Now;
                  assessmentToUpdate.AssessmentType = NewAssessmentType;
                  assessmentToUpdate.GroupWeight = 0;
                  assessmentToUpdate.IndividualWeight = 0;

                  await db.UpdateAsync(assessmentToUpdate);

                  // CLAMP EXISTING SCORES: Ensure no student or group retains a score higher than the new MaxScore
                  var overMaxScores = await db.Table<Score>()
                     .Where(s => s.AssessmentID == assessmentToUpdate.AssessmentID && s.PointsEarned > NewAssessmentMaxScore)
                     .ToListAsync();
                  foreach (var s in overMaxScores) s.PointsEarned = NewAssessmentMaxScore;
                  if (overMaxScores.Count > 0) await db.UpdateAllAsync(overMaxScores, runInTransaction: false);
               }
            }
            else
            {
               // === CREATE MODE ===
               var newAssessment = new Assessment
               {
                  ClassID = ClassId, Title = NewAssessmentTitle.Trim(), Category = SelectedCategory.Name, GradingPeriod = NewAssessmentPeriod,
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

      private void DeleteAssessment(Assessment? assessmentToDel)
      {
         if (assessmentToDel == null) return;
         DeleteAssessmentModal = new ConfirmDeleteModalViewModel(
            "Delete Assessment Column?",
            $"Are you sure you want to delete '{assessmentToDel.Title}'?\n\nThis will permanently erase all student scores and group configurations attached to this column.",
            async () =>
            {
               await _groupDbLock.WaitAsync();
               try
               {
                  var db = _dbService.GetConnection();
                  
                  var scores = await db.Table<Score>().Where(s => s.AssessmentID == assessmentToDel.AssessmentID).ToListAsync();
                  var groups = await db.Table<AssessmentGroup>().Where(g => g.AssessmentID == assessmentToDel.AssessmentID).ToListAsync();
                  var members = await db.Table<AssessmentGroupMember>().Where(m => m.AssessmentID == assessmentToDel.AssessmentID).ToListAsync();
                  
                  foreach (var s in scores) await db.DeleteAsync(s);
                  foreach (var m in members) await db.DeleteAsync(m);
                  foreach (var g in groups) await db.DeleteAsync(g);
                  await db.DeleteAsync(assessmentToDel);

                  IsDeleteAssessmentModalOpen = false;
                  DeleteAssessmentModal = null;

                  await LoadGradebookData();
                  await LoadGroupsDataAsync();
                  ShowToastMessage?.Invoke($"Successfully deleted '{assessmentToDel.Title}'.");
               }
               catch (Exception ex)
               {
                  IsDeleteAssessmentModalOpen = false;
                  DeleteAssessmentModal = null;
                  ShowToastMessage?.Invoke($"Error deleting assessment: {ex.Message}");
               }
               finally
               {
                  _groupDbLock.Release();
               }
            },
            () => { IsDeleteAssessmentModalOpen = false; DeleteAssessmentModal = null; }
         );
         IsDeleteAssessmentModalOpen = true;
      }

      private void ResetAssessmentForm()
      {
         _editingAssessmentId = null;
         NewAssessmentTitle = string.Empty;
         NewAssessmentMaxScore = 100;
         NewAssessmentDate = System.DateTime.Now;
         SelectedCategory = null;
         NewAssessmentPeriod = IsSemesterAverageView ? (GradingPeriods.FirstOrDefault() ?? "Midterm") : SelectedTermView;
         IsAddingAssessment = false;
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