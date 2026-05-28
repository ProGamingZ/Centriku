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
      #region Attendance 
         [ObservableProperty] public partial bool IsAddingRollCall { get; set; } = false;
         [ObservableProperty] public partial System.DateTime? NewRollCallDate { get; set; } = System.DateTime.Today;
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
            var targetDate = NewRollCallDate.Value.Date;
            var db = new DatabaseService().GetConnection();

            if (_editingRollCallDate.HasValue)
            {
               // === UPDATE MODE ===
               var oldDate = _editingRollCallDate.Value;
               
               // If they changed the date, check if the new date already exists!
               if (oldDate != targetDate && AttendanceDates.Contains(targetDate))
               {
                  ShowToastMessage?.Invoke("Roll call for this date already exists!");
                  return;
               }

               // Update all records that belonged to the old date
               var recordsToUpdate = await db.Table<AttendanceRecord>().Where(a => a.ClassID == ClassId && a.Date == oldDate).ToListAsync();
               foreach (var r in recordsToUpdate)
               {
                  r.Date = targetDate;
                  await db.UpdateAsync(r);
               }
            }
            else
            {
               // === CREATE MODE ===
               if (AttendanceDates.Contains(targetDate))
               {
                  ShowToastMessage?.Invoke("Roll call for this date already exists!");
                  return;
               }

               await db.InsertAsync(new AttendanceRecord { ClassID = ClassId, StudentID = "GHOST_DATE", Date = targetDate, Status = "GHOST" });

               var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
               foreach (var r in roster)
               {
                  await db.InsertAsync(new AttendanceRecord { ClassID = ClassId, StudentID = r.StudentID, Date = targetDate, Status = "P" });
               }
            }

            ResetRollCallForm();
            await LoadAttendanceData(); // Refresh the grid!
         } 
         private void EditRollCall(System.DateTime? dateParam)
         {
            if (!dateParam.HasValue) return;
            _editingRollCallDate = dateParam.Value.Date;
            NewRollCallDate = dateParam.Value.Date;
            IsAddingRollCall = true; // Slide the panel open!
         }
         private async void DeleteRollCall(System.DateTime? dateParam)
         {
            if (!dateParam.HasValue) return;
            var targetDate = dateParam.Value.Date;
            var db = new DatabaseService().GetConnection();
            
            // Delete ALL records for this class on this specific date
            var recordsToDelete = await db.Table<AttendanceRecord>().Where(a => a.ClassID == ClassId && a.Date == targetDate).ToListAsync();
            foreach (var r in recordsToDelete)
            {
               await db.DeleteAsync(r);
            }
            
            await LoadAttendanceData(); // Refresh the grid!
         }
         private void ResetRollCallForm()
      {
         _editingRollCallDate = null;
         NewRollCallDate = System.DateTime.Today;
         IsAddingRollCall = false;
      }
      #endregion

      #region Attendance Policy
         public System.Collections.ObjectModel.ObservableCollection<string> AttendanceModes { get; } = new() { "None", "Threshold", "Weighted", "Bonus" };
         [ObservableProperty] public partial string AttendanceCalculationMode { get; set; } = "None";
         [ObservableProperty] public partial int MaxAbsencesAllowed { get; set; } = 3;
         [ObservableProperty] public partial double AttendanceWeight { get; set; } = 10.0;
         [ObservableProperty] public partial double LateValue { get; set; } = 0.5;

         public bool IsThresholdMode => AttendanceCalculationMode == "Threshold";
         public bool IsWeightedOrBonusMode => AttendanceCalculationMode == "Weighted" || AttendanceCalculationMode == "Bonus";
         public bool IsMathEngineActive => AttendanceCalculationMode != "None";

         partial void OnAttendanceCalculationModeChanged(string value) 
         { 
            OnPropertyChanged(nameof(IsThresholdMode)); 
            OnPropertyChanged(nameof(IsWeightedOrBonusMode)); 
            OnPropertyChanged(nameof(IsMathEngineActive)); 
            SaveClassSettings(); 
            RecalculateFinalGrades();
         }
         partial void OnMaxAbsencesAllowedChanged(int value) { SaveClassSettings(); RecalculateFinalGrades();}
         partial void OnAttendanceWeightChanged(double value) { SaveClassSettings(); RecalculateFinalGrades();}
         partial void OnLateValueChanged(double value) { SaveClassSettings(); RecalculateFinalGrades();}
      #endregion



   }
}