using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels
{
    public partial class GradebookViewModel : ViewModelBase
    {
        [ObservableProperty] public partial int ClassId { get; set; }
        [ObservableProperty] public partial string ClassTitle { get; set; } = string.Empty;
        [ObservableProperty] public partial ObservableCollection<Student> EnrolledStudents { get; set; } = new();        
        [ObservableProperty] public partial bool IsEnrolling { get; set; } = false;
        [ObservableProperty] public partial ObservableCollection<EnrollmentItemViewModel> AvailableStudents { get; set; } = new();

        public IRelayCommand ToggleEnrollmentCommand { get; }
        public IRelayCommand SaveEnrollmentCommand { get; }
        public IRelayCommand<Student> RemoveStudentCommand { get; }

        public GradebookViewModel()
        {
            ToggleEnrollmentCommand = new RelayCommand(ToggleEnrollment);
            SaveEnrollmentCommand = new RelayCommand(SaveEnrollment);
            RemoveStudentCommand = new RelayCommand<Student>(RemoveStudent!);
        }

        public async void Initialize(int classId, string classTitle)
        {
            ClassId = classId;
            ClassTitle = classTitle;
            await LoadEnrolledStudents();
        }

        private async Task LoadEnrolledStudents()
        {
            var db = new DatabaseService().GetConnection();

            // 1. Get all roster entries for THIS specific class (Table 3)
            var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
            var studentIds = roster.Select(r => r.StudentID).ToList();

            // 2. Fetch the actual student details from the Master Directory (Table 1)
            var allStudents = await db.Table<Student>().ToListAsync();
            
            // 3. Filter down to just the enrolled ones
            var enrolled = allStudents.Where(s => studentIds.Contains(s.StudentID)).ToList();

            EnrolledStudents = new ObservableCollection<Student>(enrolled);
        }

        private async void ToggleEnrollment()
        {
            IsEnrolling = !IsEnrolling;
            
            if (IsEnrolling)
            {
                var db = new DatabaseService().GetConnection();
                var allStudents = await db.Table<Student>().ToListAsync();
                var enrolledIds = EnrolledStudents.Select(s => s.StudentID).ToList();

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
            await LoadEnrolledStudents(); // Refresh the grid
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
                await LoadEnrolledStudents();
            }
        }
    }

    public partial class EnrollmentItemViewModel : ObservableObject
    {
        public Student DbModel { get; }
        
        [ObservableProperty] public partial bool IsSelected { get; set; } = false;

        public string FullName => $"{DbModel.LastName}, {DbModel.FirstName}";
        public string StudentID => DbModel.StudentID ?? "";

        public EnrollmentItemViewModel(Student student)
        {
            DbModel = student;
        }
    }
}