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
        [ObservableProperty] public partial bool IsRemoveStudentModalOpen { get; set; } = false;
        [ObservableProperty] public partial string RemoveModalMessage { get; set; } = string.Empty;
        // ---  Modal Control Properties (Now handles Lists!) ---
        [ObservableProperty] public partial bool IsTransferStudentModalOpen { get; set; } = false;
        [ObservableProperty] public partial string TransferModalMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial ObservableCollection<TeacherClass> AvailableTransferClasses { get; set; } = new();
        [ObservableProperty] public partial TeacherClass? SelectedTransferClass { get; set; }
        
        private System.Collections.Generic.List<Student> _studentsToRemove = new();
        private System.Collections.Generic.List<Student> _studentsToTransfer = new();
        
        
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
                IsAddingAssessment = false;
                var db = new DatabaseService().GetConnection();
                var allStudents = await db.Table<Student>().ToListAsync();
                var enrolledIds = GradebookRows.Select(s => s.StudentID).ToList();

                _allAvailableStudents.Clear();
                var uniqueYears = new System.Collections.Generic.HashSet<string> { "All" };
                var uniquePrograms = new System.Collections.Generic.HashSet<string> { "All" };
                var uniqueSections = new System.Collections.Generic.HashSet<string> { "All" }; 
                var uniqueStatuses = new System.Collections.Generic.HashSet<string> { "All" };

                foreach (var s in allStudents)
                {
                    // Only show students who are NOT already enrolled in this class
                    if (s.StudentID != null && !enrolledIds.Contains(s.StudentID) && !s.IsArchived)
                    {
                        _allAvailableStudents.Add(new EnrollmentItemViewModel(s));
                        
                        if (!string.IsNullOrWhiteSpace(s.GradeYearLevel)) uniqueYears.Add(s.GradeYearLevel);
                        if (!string.IsNullOrWhiteSpace(s.Program)) uniquePrograms.Add(s.Program);
                        if (!string.IsNullOrWhiteSpace(s.SectionName)) uniqueSections.Add(s.SectionName); 
                        if (!string.IsNullOrWhiteSpace(s.EnrollmentStatus)) uniqueStatuses.Add(s.EnrollmentStatus);
                    }
                }

                EnrollmentYearFilters = new ObservableCollection<string>(uniqueYears.OrderBy(y => y == "All" ? 0 : 1).ThenBy(y => y));
                EnrollmentProgramFilters = new ObservableCollection<string>(uniquePrograms.OrderBy(p => p == "All" ? 0 : 1).ThenBy(p => p));
                EnrollmentSectionFilters = new ObservableCollection<string>(uniqueSections.OrderBy(s => s == "All" ? 0 : 1).ThenBy(s => s)); 
                EnrollmentStatusFilters = new ObservableCollection<string>(uniqueStatuses.OrderBy(s => s == "All" ? 0 : 1).ThenBy(s => s));

                SelectedEnrollmentYear = "All";
                SelectedEnrollmentProgram = "All";
                SelectedEnrollmentSection = "All"; 
                SelectedEnrollmentStatus = "All";
                
                FilterAvailableStudents();
            }
        }

        private void FilterAvailableStudents()
        {
            var filtered = _allAvailableStudents.Where(s => 
                (SelectedEnrollmentYear == "All" || s.DbModel.GradeYearLevel == SelectedEnrollmentYear) &&
                (SelectedEnrollmentProgram == "All" || s.DbModel.Program == SelectedEnrollmentProgram) &&
                (SelectedEnrollmentSection == "All" || s.DbModel.SectionName == SelectedEnrollmentSection) && 
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
            await LoadGroupsDataAsync();
            int count = selectedStudents.Count;
            string notificationMessage = count == 1 
                ? $"Successfully enrolled {selectedStudents[0].DbModel.FirstName} {selectedStudents[0].DbModel.LastName}." 
                : $"Successfully enrolled {count} students.";
            ShowToastMessage?.Invoke(notificationMessage);
        }

        // --- Modal Control Methods ---

        [RelayCommand]
        public void BulkRemoveStudents()
        {
            _studentsToRemove = GradebookRows.Where(r => r.IsSelected).Select(r => r.StudentInfo).ToList();
            if (!_studentsToRemove.Any()) { ShowToastMessage?.Invoke("Please select at least one student first."); return; }
            
            RemoveModalMessage = $"Are you sure you want to unenroll {_studentsToRemove.Count} selected student(s) from this class?\n\nThey will be removed from the class roster immediately.";
            IsRemoveStudentModalOpen = true;
        }
        [RelayCommand]
        public async Task ConfirmRemoveStudent()
        {
            if (!_studentsToRemove.Any()) return;
            var db = new DatabaseService().GetConnection();
            
            // Get all group assessments for this class so we know which groups to clean up
            var classAssessments = await db.Table<Assessment>().Where(a => a.ClassID == ClassId).ToListAsync();
            var assessmentIds = classAssessments.Select(a => a.AssessmentID).ToList();

            foreach (var student in _studentsToRemove)
            {
                // 1. Remove from Main Roster
                var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == student.StudentID).FirstOrDefaultAsync();
                if (rosterEntry != null) await db.DeleteAsync(rosterEntry);

                // 2. Remove from any Groups Tab projects in this class!
                if (assessmentIds.Count != 0)
                {
                    var groupMemberships = await db.Table<AssessmentGroupMember>()
                        .Where(m => m.StudentID == student.StudentID && assessmentIds.Contains(m.AssessmentID))
                        .ToListAsync();
                    
                    foreach (var membership in groupMemberships)
                    {
                        await db.DeleteAsync(membership);
                    }
                }
            }
            
            await LoadGradebookData();
            await LoadAttendanceData();
            await LoadRecitationData();
            await LoadGroupsDataAsync(); 
            int count = _studentsToRemove.Count;
            string notificationMessage = count == 1 
                ? $"Successfully unenrolled {_studentsToRemove[0].FirstName} {_studentsToRemove[0].LastName}." 
                : $"Successfully unenrolled {count} students.";
            // 2. Clear the list and close modal
            CancelRemoveStudent(); 
            // 3. Show the message
            ShowToastMessage?.Invoke(notificationMessage);
        }

        [RelayCommand]
        public void CancelRemoveStudent()
        {
            IsRemoveStudentModalOpen = false;
            _studentsToRemove.Clear();
            IsAllRosterSelected = false; // Reset the master checkbox
        }
    
        // --- Transfer Student Logic ---

        [RelayCommand]
        public async Task BulkTransferStudents()
        {
            _studentsToTransfer = GradebookRows.Where(r => r.IsSelected).Select(r => r.StudentInfo).ToList();
            if (!_studentsToTransfer.Any()) { ShowToastMessage?.Invoke("Please select at least one student first."); return; }
            
            await PrepareTransferModal($"Move {_studentsToTransfer.Count} selected student(s) from {ClassTitle} to another class?");
        }
        private async Task PrepareTransferModal(string message)
        {
            TransferModalMessage = message;
            var db = new DatabaseService().GetConnection();
            var allClasses = await db.Table<TeacherClass>().ToListAsync();
            AvailableTransferClasses = new ObservableCollection<TeacherClass>(allClasses.Where(c => c.ClassID != ClassId));
            SelectedTransferClass = AvailableTransferClasses.FirstOrDefault();
            IsTransferStudentModalOpen = true;
        }
        [RelayCommand]
        public async Task ConfirmTransferStudent()
        {
            if (!_studentsToTransfer.Any() || SelectedTransferClass == null) return;
            var db = new DatabaseService().GetConnection();
            
            var classAssessments = await db.Table<Assessment>().Where(a => a.ClassID == ClassId).ToListAsync();
            var assessmentIds = classAssessments.Select(a => a.AssessmentID).ToList();

            foreach (var student in _studentsToTransfer)
            {
                // 1. Swap Roster
                var oldEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == student.StudentID).FirstOrDefaultAsync();
                if (oldEntry != null) await db.DeleteAsync(oldEntry);

                await db.InsertAsync(new ClassRoster { ClassID = SelectedTransferClass.ClassID, StudentID = student.StudentID });

                // 2. Remove from any Groups Tab projects in the OLD class
                if (assessmentIds.Any())
                {
                    var groupMemberships = await db.Table<AssessmentGroupMember>()
                        .Where(m => m.StudentID == student.StudentID && assessmentIds.Contains(m.AssessmentID))
                        .ToListAsync();
                    
                    foreach (var membership in groupMemberships)
                    {
                        await db.DeleteAsync(membership);
                    }
                }
            }

            await LoadGradebookData();
            await LoadAttendanceData();
            await LoadRecitationData();
            await LoadGroupsDataAsync(); // Refresh the Groups Tab!
            
            int count = _studentsToTransfer.Count;
            string notificationMessage = count == 1 
                ? $"Successfully transferred {_studentsToTransfer[0].FirstName} {_studentsToTransfer[0].LastName}." 
                : $"Successfully transferred {count} students.";
            // 2. Clear the list and close modal
            CancelTransferStudent();
            // 3. Show the message
            ShowToastMessage?.Invoke(notificationMessage);
        }

        [RelayCommand]
        public void CancelTransferStudent()
        {
            IsTransferStudentModalOpen = false;
            _studentsToTransfer.Clear();
            IsAllRosterSelected = false; 
        }
    
    
    }
}