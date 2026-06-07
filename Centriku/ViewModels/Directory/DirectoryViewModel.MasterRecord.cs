using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
   //Handles Tab 2 (SF9), the manually entered grades, general averages, cross-subject attendance totals, and triggers the PDF export 
    public partial class DirectoryViewModel 
    {
        [ObservableProperty] public partial bool IsMasterRecordVisible { get; set; } = true;
        [ObservableProperty] public partial ObservableCollection<MasterGradeRowViewModel> MasterGrades { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<Sf9MonthlyAttendance> Sf9Attendance { get; set; } = new(); 
        
        [ObservableProperty] public partial string Q1Average { get; set; } = "--";
        [ObservableProperty] public partial string Q2Average { get; set; } = "--";
        [ObservableProperty] public partial string Q3Average { get; set; } = "--";
        [ObservableProperty] public partial string Q4Average { get; set; } = "--";
        [ObservableProperty] public partial string FinalGeneralAverage { get; set; } = "--";

        private async Task LoadMasterRecordAsync(string studentId, List<StudentClassPerformanceViewModel> activeClasses)
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.CreateTableAsync<Centriku.Models.MasterQuarterlyGrade>();
            var savedGrades = await db.Table<Centriku.Models.MasterQuarterlyGrade>().Where(g => g.StudentID == studentId).ToListAsync();

            var masterList = new List<MasterGradeRowViewModel>();

            var activeQuarterly = activeClasses.Where(c => c.EducationMode == "Quarterly").ToList();
            foreach(var aq in activeQuarterly)
            {
                string cleanVal(string val) => val.Replace("%", "").Trim();
                var row = new MasterGradeRowViewModel 
                {
                    SubjectName = aq.SubjectName,
                    Q1Text = cleanVal(aq.Q1Grade), Q2Text = cleanVal(aq.Q2Grade), Q3Text = cleanVal(aq.Q3Grade), Q4Text = cleanVal(aq.Q4Grade),
                    IsFromActiveGradebook = true, TriggerParentRecalc = RecalculateGeneralAverage
                };
                row.ForceRecalc();
                masterList.Add(row);
            }

            foreach(var sg in savedGrades)
            {
                var row = new MasterGradeRowViewModel
                {
                    GradeId = sg.GradeID, SubjectName = sg.SubjectName ?? "Unknown",
                    Q1Text = sg.Quarter1Grade?.ToString() ?? "", Q2Text = sg.Quarter2Grade?.ToString() ?? "", Q3Text = sg.Quarter3Grade?.ToString() ?? "", Q4Text = sg.Quarter4Grade?.ToString() ?? "",
                    IsFromActiveGradebook = false, TriggerParentRecalc = RecalculateGeneralAverage
                };
                row.ForceRecalc();
                masterList.Add(row);
            }

            MasterGrades = new ObservableCollection<MasterGradeRowViewModel>(masterList);
            RecalculateGeneralAverage();
        }

        private async Task LoadSf9AttendanceAsync(string studentId)
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var allAttendance = await db.Table<Centriku.Models.AttendanceRecord>().Where(a => a.StudentID == studentId).ToListAsync();

            var sf9AttList = new List<Sf9MonthlyAttendance>();
            int[] monthOrder = [8, 9, 10, 11, 12, 1, 2, 3, 4, 5]; 
            string[] monthNames = ["Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr", "May"];

            for (int i = 0; i < monthOrder.Length; i++)
            {
                int m = monthOrder[i];
                var monthRecords = allAttendance.Where(a => a.Date.Month == m).ToList();
                var uniqueDates = monthRecords.GroupBy(a => a.Date.Date).ToList();

                int present = 0; int absent = 0;
                foreach (var dateGroup in uniqueDates)
                {
                    if (dateGroup.Any(r => r.Status == "P" || r.Status == "L")) present++;
                    else absent++;
                }

                sf9AttList.Add(new Sf9MonthlyAttendance { Month = monthNames[i], MonthNum = m, DaysPresent = present, DaysAbsent = absent, SchoolDays = present + absent });
            }
            Sf9Attendance = new ObservableCollection<Sf9MonthlyAttendance>(sf9AttList);
        }

        private void RecalculateGeneralAverage()
        {
            double CalculateColumnAverage(Func<MasterGradeRowViewModel, string> selector)
            {
                var validRows = MasterGrades.Where(r => double.TryParse(selector(r), out _)).ToList();
                if (!validRows.Any()) return -1;
                return validRows.Sum(r => double.Parse(selector(r))) / validRows.Count;
            }

            double q1 = CalculateColumnAverage(r => r.Q1Text); double q2 = CalculateColumnAverage(r => r.Q2Text);
            double q3 = CalculateColumnAverage(r => r.Q3Text); double q4 = CalculateColumnAverage(r => r.Q4Text);
            double fin = CalculateColumnAverage(r => r.FinalGrade);

            Q1Average = q1 >= 0 ? q1.ToString("0.##") : "--"; Q2Average = q2 >= 0 ? q2.ToString("0.##") : "--";
            Q3Average = q3 >= 0 ? q3.ToString("0.##") : "--"; Q4Average = q4 >= 0 ? q4.ToString("0.##") : "--";
            FinalGeneralAverage = fin >= 0 ? fin.ToString("0.##") : "--";
        }

        private async void DeleteMasterSubject(MasterGradeRowViewModel row)
        {
            if (row == null || row.IsFromActiveGradebook) return; 
            MasterGrades.Remove(row); 
            if (row.GradeId != 0) 
            {
                var db = new Centriku.Services.DatabaseService().GetConnection();
                var dbRecord = await db.Table<Centriku.Models.MasterQuarterlyGrade>().Where(g => g.GradeID == row.GradeId).FirstOrDefaultAsync();
                if (dbRecord != null) await db.DeleteAsync(dbRecord);
            }
            RecalculateGeneralAverage(); 
        }

        private void AddBlankMasterSubject() => MasterGrades.Add(new MasterGradeRowViewModel { SubjectName = "New Subject", IsFromActiveGradebook = false, TriggerParentRecalc = RecalculateGeneralAverage });

        private async void SaveMasterRecord()
        {
            if (SelectedProfile == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var existingRecords = await db.Table<Centriku.Models.MasterQuarterlyGrade>().Where(g => g.StudentID == SelectedProfile.StudentID).ToListAsync();

            foreach(var row in MasterGrades)
            {
                if (row.IsFromActiveGradebook) continue; 

                var dbRecord = existingRecords.FirstOrDefault(e => e.GradeID == row.GradeId);
                if (dbRecord != null)
                {
                    dbRecord.SubjectName = row.SubjectName;
                    dbRecord.Quarter1Grade = double.TryParse(row.Q1Text, out double q1) ? q1 : null;
                    dbRecord.Quarter2Grade = double.TryParse(row.Q2Text, out double q2) ? q2 : null;
                    dbRecord.Quarter3Grade = double.TryParse(row.Q3Text, out double q3) ? q3 : null;
                    dbRecord.Quarter4Grade = double.TryParse(row.Q4Text, out double q4) ? q4 : null;
                    await db.UpdateAsync(dbRecord);
                }
                else
                {
                    var newRec = new Centriku.Models.MasterQuarterlyGrade
                    {
                        StudentID = SelectedProfile.StudentID, SubjectName = row.SubjectName,
                        Quarter1Grade = double.TryParse(row.Q1Text, out double nq1) ? nq1 : null,
                        Quarter2Grade = double.TryParse(row.Q2Text, out double nq2) ? nq2 : null,
                        Quarter3Grade = double.TryParse(row.Q3Text, out double nq3) ? nq3 : null,
                        Quarter4Grade = double.TryParse(row.Q4Text, out double nq4) ? nq4 : null
                    };
                    await db.InsertAsync(newRec);
                    row.GradeId = newRec.GradeID; 
                }
            }
        }

        private async void GenerateSf9()
        {
            if (SelectedProfile == null) return;
            
            // 1. Fetch the absolute latest SF9 Settings right before printing!
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var settings = await db.Table<Centriku.Models.AppSettings>().FirstOrDefaultAsync() ?? new Centriku.Models.AppSettings();

            // 2. Apply the Custom Naming Format
            string safeName = settings.Sf9FileNamingFormat
                .Replace("[LastName]", SelectedProfile.LastName ?? "")
                .Replace("[FirstName]", SelectedProfile.FirstName ?? "")
                .Replace("[LRN]", SelectedProfile.StudentID ?? "")
                .Replace(" ", "_");

            // 3. Apply the Custom Save Folder
            string exportFolder = string.IsNullOrWhiteSpace(settings.Sf9DefaultExportPath) 
                ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop) 
                : settings.Sf9DefaultExportPath;

            string fullPath = System.IO.Path.Combine(exportFolder, $"{safeName}.pdf");

            try
            {
                // Pass the settings into the Generator!
                Centriku.Services.Sf9Generator.GenerateReportCard(this, fullPath, settings);
                
                // 4. Obey the Auto-Open rule
                if (settings.Sf9AutoOpenPdf)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullPath) { UseShellExecute = true });
                }
            }
            catch (Exception ex) { Console.WriteLine($"Failed to generate PDF: {ex.Message}"); }
        }
    }
}