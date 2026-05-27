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

        public int TotalP => Cells.Values.Count(c => c.Status == "P");
        public int TotalL => Cells.Values.Count(c => c.Status == "L");
        public int TotalA => Cells.Values.Count(c => c.Status == "A");

        public void RefreshTotals()
        {
            OnPropertyChanged(nameof(TotalP));
            OnPropertyChanged(nameof(TotalL));
            OnPropertyChanged(nameof(TotalA));
        }

        public AttendanceGridRowViewModel(Student student)
        {
            StudentInfo = student;
        }
    }
}