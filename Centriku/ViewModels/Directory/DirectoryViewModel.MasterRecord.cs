using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
    public partial class DirectoryViewModel 
    {
        [ObservableProperty] public partial bool IsMasterRecordVisible { get; set; } = true;
        [ObservableProperty] public partial ObservableCollection<MasterGradeRowViewModel> MasterGrades { get; set; } = new();
        
        [ObservableProperty] public partial string MidtermAverage { get; set; } = "--";
        [ObservableProperty] public partial string FinalTermAverage { get; set; } = "--";
        [ObservableProperty] public partial string FinalGeneralAverage { get; set; } = "--";

        private async Task LoadMasterRecordAsync(string studentId, List<StudentClassPerformanceViewModel> activeClasses)
        {
            // Yield to UI slightly
            await Task.Delay(10); 
            
            var masterList = new List<MasterGradeRowViewModel>();

            // ONLY load grades from the classes the teacher is actively teaching
            foreach(var aq in activeClasses)
            {
                string cleanVal(string val) => val.Replace("%", "").Trim();
                var row = new MasterGradeRowViewModel 
                {
                    SubjectName = aq.SubjectName,
                    MidtermText = cleanVal(aq.MidtermGrade), FinalTermText = cleanVal(aq.FinalTermGrade),
                    IsFromActiveGradebook = true, TriggerParentRecalc = RecalculateGeneralAverage
                };
                row.ForceRecalc();
                masterList.Add(row);
            }

            MasterGrades = new ObservableCollection<MasterGradeRowViewModel>(masterList);
            RecalculateGeneralAverage();
        }

        private void RecalculateGeneralAverage()
        {
            double CalculateColumnAverage(Func<MasterGradeRowViewModel, string> selector)
            {
                var validRows = MasterGrades.Where(r => double.TryParse(selector(r), out _)).ToList();
                if (!validRows.Any()) return -1;
                return validRows.Sum(r => double.Parse(selector(r))) / validRows.Count;
            }

            double mid = CalculateColumnAverage(r => r.MidtermText); 
            double finTerm = CalculateColumnAverage(r => r.FinalTermText);
            double fin = CalculateColumnAverage(r => r.FinalGrade);

            MidtermAverage = mid >= 0 ? mid.ToString("0.##") : "--"; 
            FinalTermAverage = finTerm >= 0 ? finTerm.ToString("0.##") : "--";
            FinalGeneralAverage = fin >= 0 ? fin.ToString("0.##") : "--";
        }

        // We leave these methods empty to prevent UI Binding crashes, 
        // but they do nothing now since the manual database table was deleted!
        private void DeleteMasterSubject(MasterGradeRowViewModel row) { }
        private void AddBlankMasterSubject() { }
        private void SaveMasterRecord() { }
    }
}