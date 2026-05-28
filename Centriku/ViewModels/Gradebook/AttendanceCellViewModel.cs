using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;

namespace Centriku.ViewModels
{
    public partial class AttendanceCellViewModel : ObservableObject
    {
        public AttendanceRecord DbModel { get; }
        private readonly System.Action _onStatusChanged;
        private readonly System.Action<string> _showToast;

        public string Status
        {
            get => DbModel.Status ?? "";
            set
            {
                string input = value?.ToUpper() ?? ""; 
                
                if (input == "P" || input == "L" || input == "A" || input == "E" || input == "")
                {
                    if (DbModel.Status != input)
                    {
                        DbModel.Status = input;
                        OnPropertyChanged();
                        SaveToDb();
                        _onStatusChanged?.Invoke(); 
                    }
                }
                else
                {
                    OnPropertyChanged(); 
                    _showToast?.Invoke($"'{input}' is invalid. Only use P, L, A, or E.");
                }
            }
        }

        public string Reason
        {
            get => DbModel.Reason ?? "";
            set
            {
                if (DbModel.Reason != value)
                {
                    DbModel.Reason = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasReason));
                    SaveToDb(); // Instantly saves notes to SQLite!
                }
            }
        }
        public bool HasReason => !string.IsNullOrWhiteSpace(Reason);
        public AttendanceCellViewModel(AttendanceRecord record, System.Action onStatusChanged, System.Action<string> showToast)
        {
            DbModel = record;
            _onStatusChanged = onStatusChanged;
            _showToast = showToast;
        }

        private async void SaveToDb()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            if (DbModel.RecordID == 0) await db.InsertAsync(DbModel);
            else await db.UpdateAsync(DbModel);
        }
    }
}