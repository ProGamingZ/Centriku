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
        [ObservableProperty] public partial bool IsEnrolling { get; set; } = false;
        
        // Full list of students fetched from DB
        private System.Collections.Generic.List<EnrollmentItemViewModel> _allAvailableStudents = new();
        
        // The filtered list bound to the UI
        [ObservableProperty] public partial ObservableCollection<EnrollmentItemViewModel> AvailableStudents { get; set; } = new();
        
        // Filter Options
        [ObservableProperty] public partial ObservableCollection<string> EnrollmentYearFilters { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<string> EnrollmentProgramFilters { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<string> EnrollmentSectionFilters { get; set; } = new(); 
        [ObservableProperty] public partial ObservableCollection<string> EnrollmentStatusFilters { get; set; } = new();

        // Selected Filters
        [ObservableProperty] public partial string SelectedEnrollmentYear { get; set; } = "All";
        [ObservableProperty] public partial string SelectedEnrollmentProgram { get; set; } = "All";
        [ObservableProperty] public partial string SelectedEnrollmentSection { get; set; } = "All"; 
        [ObservableProperty] public partial string SelectedEnrollmentStatus { get; set; } = "All";

        // Trigger filtering when selections change
        partial void OnSelectedEnrollmentYearChanged(string value) => FilterAvailableStudents();
        partial void OnSelectedEnrollmentProgramChanged(string value) => FilterAvailableStudents();
        partial void OnSelectedEnrollmentSectionChanged(string value) => FilterAvailableStudents(); 
        partial void OnSelectedEnrollmentStatusChanged(string value) => FilterAvailableStudents();

        public IRelayCommand ToggleEnrollmentCommand { get; }
        public IRelayCommand SaveEnrollmentCommand { get; }
        public IRelayCommand<Student> RemoveStudentCommand { get; }

        // NEW: Select/Deselect All Commands
        [RelayCommand]
        public void SelectAllStudents()
        {
            foreach (var student in AvailableStudents) { student.IsSelected = true; }
        }

        [RelayCommand]
        public void DeselectAllStudents()
        {
            foreach (var student in AvailableStudents) { student.IsSelected = false; }
        }

        private async void ToggleEnrollment()
        {
            IsEnrolling = !IsEnrolling;
            
            if (IsEnrolling)
            {
                var db = new DatabaseService().GetConnection();
                var allStudents = await db.Table<Student>().ToListAsync();
                var enrolledIds = GradebookRows.Select(s => s.StudentID).ToList();

                _allAvailableStudents.Clear();
                var uniqueYears = new System.Collections.Generic.HashSet<string> { "All" };
                var uniquePrograms = new System.Collections.Generic.HashSet<string> { "All" };
                var uniqueSections = new System.Collections.Generic.HashSet<string> { "All" }; // NEW
                var uniqueStatuses = new System.Collections.Generic.HashSet<string> { "All" };

                foreach (var s in allStudents)
                {
                    // Only show students who are NOT already enrolled in this class
                    if (s.StudentID != null && !enrolledIds.Contains(s.StudentID) && !s.IsArchived)
                    {
                        _allAvailableStudents.Add(new EnrollmentItemViewModel(s));
                        
                        if (!string.IsNullOrWhiteSpace(s.GradeYearLevel)) uniqueYears.Add(s.GradeYearLevel);
                        if (!string.IsNullOrWhiteSpace(s.Program)) uniquePrograms.Add(s.Program);
                        if (!string.IsNullOrWhiteSpace(s.SectionName)) uniqueSections.Add(s.SectionName); // NEW
                        if (!string.IsNullOrWhiteSpace(s.EnrollmentStatus)) uniqueStatuses.Add(s.EnrollmentStatus);
                    }
                }

                EnrollmentYearFilters = new ObservableCollection<string>(uniqueYears.OrderBy(y => y == "All" ? 0 : 1).ThenBy(y => y));
                EnrollmentProgramFilters = new ObservableCollection<string>(uniquePrograms.OrderBy(p => p == "All" ? 0 : 1).ThenBy(p => p));
                EnrollmentSectionFilters = new ObservableCollection<string>(uniqueSections.OrderBy(s => s == "All" ? 0 : 1).ThenBy(s => s)); // NEW
                EnrollmentStatusFilters = new ObservableCollection<string>(uniqueStatuses.OrderBy(s => s == "All" ? 0 : 1).ThenBy(s => s));

                SelectedEnrollmentYear = "All";
                SelectedEnrollmentProgram = "All";
                SelectedEnrollmentSection = "All"; // NEW
                SelectedEnrollmentStatus = "All";
                
                FilterAvailableStudents();
            }
        }

        private void FilterAvailableStudents()
        {
            var filtered = _allAvailableStudents.Where(s => 
                (SelectedEnrollmentYear == "All" || s.DbModel.GradeYearLevel == SelectedEnrollmentYear) &&
                (SelectedEnrollmentProgram == "All" || s.DbModel.Program == SelectedEnrollmentProgram) &&
                (SelectedEnrollmentSection == "All" || s.DbModel.SectionName == SelectedEnrollmentSection) && // NEW
                (SelectedEnrollmentStatus == "All" || s.DbModel.EnrollmentStatus == SelectedEnrollmentStatus)
            ).ToList();

            AvailableStudents = new ObservableCollection<EnrollmentItemViewModel>(filtered);
        }

        private async void SaveEnrollment()
        {
            var db = new DatabaseService().GetConnection();
            
            // We check the full list so selections aren't lost if a filter hides them
            var selectedStudents = _allAvailableStudents.Where(s => s.IsSelected).ToList();

            foreach (var student in selectedStudents)
            {
                var newRosterEntry = new ClassRoster
                {
                    ClassID = ClassId, 
                    StudentID = student.DbModel.StudentID 
                };
                await db.InsertAsync(newRosterEntry); 
            }

            IsEnrolling = false;
            await LoadGradebookData(); 
            await LoadAttendanceData();
            await LoadRecitationData(); 
        }

        private async void RemoveStudent(Student student)
        {
            if (student == null) return;
            var db = new DatabaseService().GetConnection();
            
            var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == student.StudentID).FirstOrDefaultAsync();
            if (rosterEntry != null)
            {
                await db.DeleteAsync(rosterEntry);
                await LoadGradebookData();
                await LoadAttendanceData();
                await LoadRecitationData();
            }
        }
    }
}