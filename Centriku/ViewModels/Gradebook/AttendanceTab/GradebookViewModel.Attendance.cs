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
         partial void OnShowTotalEChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }

         public IRelayCommand ToggleAddRollCallCommand { get; }
         public IRelayCommand SaveRollCallCommand { get; }
         public IRelayCommand<System.DateTime?> EditRollCallCommand { get; }
         public IRelayCommand<System.DateTime?> DeleteRollCallCommand { get; } 

         private async void SaveRollCallDay()
         {
            if (!NewRollCallDate.HasValue || NewRollCallHours <= 0) return;
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
                     r.SessionHours = NewRollCallHours; // Persist updated session hours!
                  }
                  
                  if (recordsToUpdate.Count > 0)
                     await db.UpdateAllAsync(recordsToUpdate, runInTransaction: false);

                  ShowToastMessage?.Invoke($"Successfully updated roll call for {targetDate:MMM dd, yyyy}.");
               }
               else
               {
                  if (AttendanceDates.Contains(targetDate))
                  {
                     ShowToastMessage?.Invoke("Roll call for this date already exists!");
                     return;
                  }

                  var newRecords = new System.Collections.Generic.List<AttendanceRecord>
                  {
                     new AttendanceRecord { ClassID = ClassId, StudentID = "GHOST_DATE", Date = targetDate, Status = "GHOST", SessionHours = NewRollCallHours }
                  };

                  var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
                  foreach (var r in roster)
                  {
                     newRecords.Add(new AttendanceRecord { ClassID = ClassId, StudentID = r.StudentID, Date = targetDate, Status = "P", SessionHours = NewRollCallHours });
                  }

                  await db.InsertAllAsync(newRecords, runInTransaction: false);
                  ShowToastMessage?.Invoke($"Successfully created roll call for {targetDate:MMM dd, yyyy}.");
               }

               ResetRollCallForm();
               await LoadAttendanceData(); 
            }
            catch (Exception ex)
            {
               ShowToastMessage?.Invoke($"Error saving roll call: {ex.Message}");
            }
            finally { IsProcessing = false; }
         }

         private async void EditRollCall(System.DateTime? dateParam)
         {
            if (!dateParam.HasValue) return;
            var targetDate = dateParam.Value.Date;
            _editingRollCallDate = targetDate;
            NewRollCallDate = targetDate;

            // Load the existing SessionHours for this date
            string dateKey = targetDate.ToString("yyyy-MM-dd");
            var firstRow = AttendanceGridRows.FirstOrDefault();
            if (firstRow != null && firstRow.Cells.TryGetValue(dateKey, out var cellVm) && cellVm.DbModel.SessionHours > 0)
            {
               NewRollCallHours = cellVm.DbModel.SessionHours;
            }
            else
            {
               var db = _dbService.GetConnection();
               var existingRecord = await db.Table<AttendanceRecord>().Where(a => a.ClassID == ClassId && a.Date == targetDate).FirstOrDefaultAsync();
               NewRollCallHours = existingRecord != null && existingRecord.SessionHours > 0 ? existingRecord.SessionHours : 1.5;
            }

            IsAddingRollCall = true;
         }

         [ObservableProperty] public partial bool IsDeleteRollCallModalOpen { get; set; } = false;
         [ObservableProperty] public partial string DeleteRollCallMessage { get; set; } = string.Empty;
         [ObservableProperty] public partial ConfirmDeleteModalViewModel? DeleteRollCallModal { get; set; }

         private void DeleteRollCall(System.DateTime? dateParam)
         {
            if (!dateParam.HasValue) return;
            var targetDate = dateParam.Value.Date;
            
            DeleteRollCallModal = new ConfirmDeleteModalViewModel(
                "Delete Attendance Column?",
                $"Are you sure you want to delete the attendance column for {targetDate:MMM dd, yyyy}?\n\nThis will permanently erase the attendance records of all students for this date.",
                async () => 
                {
                    await _groupDbLock.WaitAsync();
                    try
                    {
                        var db = _dbService.GetConnection();
                        var recordsToDelete = await db.Table<AttendanceRecord>().Where(a => a.ClassID == ClassId && a.Date == targetDate).ToListAsync();
                        
                        foreach (var r in recordsToDelete) await db.DeleteAsync(r);

                        if (SelectedAttendanceDateDisplay == targetDate.ToString("MMM dd, yyyy"))
                        {
                            IsAttendancePanelOpen = false;
                            SelectedAttendanceCell = null;
                        }

                        IsDeleteRollCallModalOpen = false;
                        DeleteRollCallModal = null;
                        
                        await LoadAttendanceData(); 
                        ShowToastMessage?.Invoke($"Deleted roll call for {targetDate:MMM dd, yyyy}.");
                    }
                    catch (Exception ex)
                    {
                        IsDeleteRollCallModalOpen = false;
                        DeleteRollCallModal = null;
                        ShowToastMessage?.Invoke($"Error deleting roll call: {ex.Message}");
                    }
                    finally
                    {
                        _groupDbLock.Release();
                    }
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
                 try
                 {
                     var db = _dbService.GetConnection();
                     await db.UpdateAsync(FdaSettingsClass);
                     _currentClass = FdaSettingsClass; // Update cached instance for MathEngine
                     IsFdaModalOpen = false;
                     
                     ShowToastMessage?.Invoke("Attendance FDA Rules updated.");
                     RecalculateFinalGrades();
                 }
                 finally
                 {
                     IsProcessing = false;
                 }
             }
         }

         [RelayCommand] public void CloseFdaModal() => IsFdaModalOpen = false;

      #endregion
   }
}