using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
   // Holds the data structures that the ViewModel uses
    public class Sf9MonthlyAttendance
    {
        public string Month { get; set; } = string.Empty;
        public int MonthNum { get; set; }
        public int SchoolDays { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
    }

    public partial class StudentRowViewModel(Centriku.Models.Student student) : ObservableObject
    {
        public Centriku.Models.Student DbModel { get; } = student;

        [ObservableProperty] public partial bool IsEditing { get; set; } = false;

        public string StudentID { get => DbModel.StudentID ?? string.Empty; set { DbModel.StudentID = value; OnPropertyChanged(); } }
        public string FirstName { get => DbModel.FirstName ?? string.Empty; set { DbModel.FirstName = value; OnPropertyChanged(); } }
        public string LastName { get => DbModel.LastName ?? string.Empty; set { DbModel.LastName = value; OnPropertyChanged(); } }
        public string MiddleName { get => DbModel.MiddleName ?? string.Empty; set { DbModel.MiddleName = value; OnPropertyChanged(); } }
        public string Suffix { get => DbModel.Suffix ?? string.Empty; set { DbModel.Suffix = value; OnPropertyChanged(); } }
        public string Gender { get => DbModel.Gender ?? string.Empty; set { DbModel.Gender = value; OnPropertyChanged(); } }
        public string GradeYearLevel { get => DbModel.GradeYearLevel ?? string.Empty; set { DbModel.GradeYearLevel = value; OnPropertyChanged(); } }
        public string Program { get => DbModel.Program ?? string.Empty; set { DbModel.Program = value; OnPropertyChanged(); } }
        public string SectionName { get => DbModel.SectionName ?? string.Empty; set { DbModel.SectionName = value; OnPropertyChanged(); } }
        public string EnrollmentStatus { get => DbModel.EnrollmentStatus ?? string.Empty; set { DbModel.EnrollmentStatus = value; OnPropertyChanged(); } }
    }

    public partial class StudentClassPerformanceViewModel : ObservableObject
    {
        public string SubjectName { get; set; } = string.Empty;
        public string Term { get; set; } = string.Empty;
        
        public string MidtermGrade { get; set; } = "--";
        public string FinalTermGrade { get; set; } = "--";
        public string SemesterAverage { get; set; } = "--";

        public int GradedTasksCount { get; set; }
        public string AverageScorePercentage { get; set; } = "--";
        public int Absences { get; set; }
        public int Lates { get; set; }
    }

}