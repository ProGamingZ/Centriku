using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;
using System;
using Centriku.ViewModels.Modals;

namespace Centriku.ViewModels
{
   public partial class GradebookViewModel
   {
      #region Attendance 
         
         [ObservableProperty] public partial bool IsAddingRollCall { get; set; } = false;
         [ObservableProperty] public partial System.DateTime? NewRollCallDate { get; set; } = System.DateTime.Today;
         [ObservableProperty] public partial double NewRollCallHours { get; set; } = 1.5;
         private System.DateTime? _editingRollCallDate = null;
         [ObservableProperty] public partial ObservableCollection<string> AvailableMonths { get; set; } = new();
         [ObservableProperty] public partial string SelectedMonthFilter { get; set; } = "All Months";
         partial void OnSelectedMonthFilterChanged(string value) { TriggerGridRedraw(); }
         [ObservableProperty] public partial bool ShowTotalP { get; set; } = true;
         [ObservableProperty] public partial bool ShowTotalL { get; set; } = true;
         [ObservableProperty] public partial bool ShowTotalA { get; set; } = true;
         [ObservableProperty] public partial bool ShowTotalE { get; set; } = true; 
         partial void OnShowTotalPChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
         partial void OnShowTotalLChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
         partial void OnShowTotalAChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
         partial void OnShowTotalEChanged(bool value) { TriggerGridRedraw(); }
         public IRelayCommand ToggleAddRollCallCommand { get; }
         public IRelayCommand SaveRollCallCommand { get; }
         public IRelayCommand<System.DateTime?> EditRollCallCommand { get;}
         public IRelayCommand<System.DateTime?> DeleteRollCallCommand { get;} 
         private async void SaveRollCallDay()
         {
            if (!NewRollCallDate.HasValue) return;
            IsProcessing = true;
            
            try 
            {
               var targetDate = NewRollCallDate.Value.Date;
               var db = _dbService.GetConnection();

               if (_editingRollCallDate.HasValue)
               {
                  var oldDate = _editingRollCallDate.Value;
                  if (oldDate != targetDate && AttendanceDates.Contains(targetDate))
                  {
                     ShowToastMessage?.Invoke("Roll call for this date already exists!");
                     return;
                  }

                  var recordsToUpdate = await db.Table<AttendanceRecord>().Where(a => a.ClassID == ClassId && a.Date == oldDate).ToListAsync();
                  foreach (var r in recordsToUpdate)
                  {
                     r.Date = targetDate;
                  }
                  
                  // BULK UPDATE
                  await db.UpdateAllAsync(recordsToUpdate);
                  ShowToastMessage?.Invoke($"Successfully moved roll call to {targetDate:MMM dd, yyyy}.");
               }
               else
               {
                  if (AttendanceDates.Contains(targetDate))
                  {
                     ShowToastMessage?.Invoke("Roll call for this date already exists!");
                     return;
                  }

                  // Build a list in memory
                  var newRecords = new System.Collections.Generic.List<AttendanceRecord>
                  {
                     new AttendanceRecord { ClassID = ClassId, StudentID = "GHOST_DATE", Date = targetDate, Status = "GHOST", SessionHours = NewRollCallHours }
                  };

                  var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
                  foreach (var r in roster)
                  {
                     newRecords.Add(new AttendanceRecord { ClassID = ClassId, StudentID = r.StudentID, Date = targetDate, Status = "P", SessionHours = NewRollCallHours });
                  }

                  // BULK INSERT
                  await db.InsertAllAsync(newRecords);
                  ShowToastMessage?.Invoke($"Successfully created roll call for {targetDate:MMM dd, yyyy}.");
               }

               ResetRollCallForm();
               await LoadAttendanceData(); 
            }
            finally { IsProcessing = false; }
         }
         private void EditRollCall(System.DateTime? dateParam)
         {
            if (!dateParam.HasValue) return;
            _editingRollCallDate = dateParam.Value.Date;
            NewRollCallDate = dateParam.Value.Date;
            IsAddingRollCall = true; // Slide the panel open!
         }
         [ObservableProperty] public partial bool IsDeleteRollCallModalOpen { get; set; } = false;
         [ObservableProperty] public partial string DeleteRollCallMessage { get; set; } = string.Empty;
         [ObservableProperty] public partial ConfirmDeleteModalViewModel? DeleteRollCallModal { get; set; }

         private void DeleteRollCall(System.DateTime? dateParam)
         {
            if (!dateParam.HasValue) return;
            var targetDate = dateParam.Value.Date;
            
            // Inject the specific logic directly into the universal modal
            DeleteRollCallModal = new ConfirmDeleteModalViewModel(
                "Delete Attendance Column?",
                $"Are you sure you want to delete the attendance column for {targetDate:MMM dd, yyyy}?\n\nThis will permanently erase the attendance records of all students for this date.",
                async () => 
                {
                    var db = _dbService.GetConnection();
                    var recordsToDelete = await db.Table<AttendanceRecord>().Where(a => a.ClassID == ClassId && a.Date == targetDate).ToListAsync();
                    
                    if (recordsToDelete.Any())
                    {
                        await db.RunInTransactionAsync(tran => 
                        {
                            foreach (var r in recordsToDelete) tran.Delete(r);
                        });
                    }
                    
                    await LoadAttendanceData(); 
                    ShowToastMessage?.Invoke($"Deleted roll call for {targetDate:MMM dd, yyyy}.");
                },
                () => { IsDeleteRollCallModalOpen = false; DeleteRollCallModal = null; }
            );

            IsDeleteRollCallModalOpen = true;
         }

         private void ResetRollCallForm()
         {
            _editingRollCallDate = null;
            NewRollCallDate = System.DateTime.Today;
            IsAddingRollCall = false;
            NewRollCallHours = 1.5;
         }

         [ObservableProperty] public partial bool IsFdaModalOpen { get; set; } = false;
         [ObservableProperty] public partial TeacherClass? FdaSettingsClass { get; set; }
         
         // Helper for UI Radio Buttons
         public bool IsFdaEquivalentRule 
         { 
             get => FdaSettingsClass?.FdaRuleType == "Equivalent";
             set { if (value && FdaSettingsClass != null) { FdaSettingsClass.FdaRuleType = "Equivalent"; OnPropertyChanged(); OnPropertyChanged(nameof(IsFdaIndependentRule)); } }
         }
         public bool IsFdaIndependentRule 
         { 
             get => FdaSettingsClass?.FdaRuleType == "Independent";
             set { if (value && FdaSettingsClass != null) { FdaSettingsClass.FdaRuleType = "Independent"; OnPropertyChanged(); OnPropertyChanged(nameof(IsFdaEquivalentRule)); } }
         }

         [RelayCommand]
         public async Task OpenFdaSettingsAsync()
         {
             var db = _dbService.GetConnection();
             FdaSettingsClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
             if (FdaSettingsClass != null)
             {
                 OnPropertyChanged(nameof(IsFdaEquivalentRule));
                 OnPropertyChanged(nameof(IsFdaIndependentRule));
                 IsFdaModalOpen = true;
             }
         }

         [RelayCommand]
         public async Task SaveFdaSettingsAsync()
         {
             if (FdaSettingsClass != null)
             {
                 IsProcessing = true;
                 var db = _dbService.GetConnection();
                 await db.UpdateAsync(FdaSettingsClass);
                 IsProcessing = false;
                 IsFdaModalOpen = false;
                 
                 ShowToastMessage?.Invoke("Attendance FDA Rules updated.");
                 RecalculateFinalGrades(); // Re-run the math engine!
             }
         }
         [RelayCommand] public void CloseFdaModal() => IsFdaModalOpen = false;

      #endregion

      
   }
}