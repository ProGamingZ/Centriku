using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;

namespace Centriku.ViewModels
{
    public partial class AttendanceGridRowViewModel : ObservableObject
    {
        public Student StudentInfo { get; }
        public string LastName => StudentInfo.LastName ?? "";
        public string FirstName => StudentInfo.FirstName ?? "";

        public System.Collections.Generic.Dictionary<string, AttendanceCellViewModel> Cells { get; set; } = [];

        public double TotalP => Cells.Values.Where(c => c.Status == "P").Sum(c => c.DbModel.SessionHours);
        public double TotalL => Cells.Values.Where(c => c.Status == "L").Sum(c => c.DbModel.SessionHours);
        public double TotalA => Cells.Values.Where(c => c.Status == "A").Sum(c => c.DbModel.SessionHours);
        public double TotalE => Cells.Values.Where(c => c.Status == "E").Sum(c => c.DbModel.SessionHours);
        
        public void RefreshTotals()
        {
            OnPropertyChanged(nameof(TotalP));
            OnPropertyChanged(nameof(TotalL));
            OnPropertyChanged(nameof(TotalA));
            OnPropertyChanged(nameof(TotalE));
        }
        [ObservableProperty] public partial string RemainingAllowanceText { get; set; } = "--";
        public AttendanceGridRowViewModel(Student student)
        {
            StudentInfo = student;
        }
    }
}