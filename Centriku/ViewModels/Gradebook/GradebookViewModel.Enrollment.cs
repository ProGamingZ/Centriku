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
        [ObservableProperty] public partial ObservableCollection<EnrollmentItemViewModel> AvailableStudents { get; set; } = new();
        public IRelayCommand ToggleEnrollmentCommand { get; }
        public IRelayCommand SaveEnrollmentCommand { get; }
        public IRelayCommand<Student> RemoveStudentCommand { get; }

        private async void ToggleEnrollment()
        {
            IsEnrolling = !IsEnrolling;
            
            if (IsEnrolling)
            {
                var db = new DatabaseService().GetConnection();
                var allStudents = await db.Table<Student>().ToListAsync();
                var enrolledIds = GradebookRows.Select(s => s.StudentID).ToList();

                AvailableStudents.Clear();
                foreach (var s in allStudents)
                {
                    // Only show students who are NOT already enrolled in this class
                    if (s.StudentID != null && !enrolledIds.Contains(s.StudentID))
                    {
                        AvailableStudents.Add(new EnrollmentItemViewModel(s));
                    }
                }
            }
        }
        private async void SaveEnrollment()
        {
            var db = new DatabaseService().GetConnection();
            
            var selectedStudents = AvailableStudents.Where(s => s.IsSelected).ToList();

            foreach (var student in selectedStudents)
            {
                var newRosterEntry = new ClassRoster
                {
                    ClassID = ClassId, // The class we are currently viewing
                    StudentID = student.DbModel.StudentID // The student we just checked
                };
                await db.InsertAsync(newRosterEntry); // Link them in Table 3!
            }

            IsEnrolling = false;
            await LoadGradebookData(); // Refresh the grid
            await LoadAttendanceData();
        }
        private async void RemoveStudent(Student student)
                {
                    if (student == null) return;
                    var db = new DatabaseService().GetConnection();
                    
                    // Find the exact link in Table 3 and sever it
                    var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == student.StudentID).FirstOrDefaultAsync();
                    if (rosterEntry != null)
                    {
                        await db.DeleteAsync(rosterEntry);
                        await LoadGradebookData();
                        await LoadAttendanceData();
                    }
                }

    }
}