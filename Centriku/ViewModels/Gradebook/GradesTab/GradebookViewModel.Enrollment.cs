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
            if (IsEnrolling)
            {
                IsEnrolling = false; // Just close it if it's already open
                return;
            }
            
            IsEnrolling = true;
            IsAddingAssessment = false;
            IsProcessing = true; // Turn on the loading spinner instantly!

            try
            {
                // Give the Avalonia UI thread a microsecond to draw the spinner before we lock the CPU
                await Task.Delay(50); 

                var db = new DatabaseService().GetConnection();
                var allStudents = await db.Table<Student>().ToListAsync();
                
                // O(1) HASHSET: Instant memory lookup instead of a slow loop
                var enrolledIds = new System.Collections.Generic.HashSet<string>(GradebookRows.Select(s => s.StudentID));

                _allAvailableStudents.Clear();
                var uniqueYears = new System.Collections.Generic.HashSet<string> { "All" };
                var uniquePrograms = new System.Collections.Generic.HashSet<string> { "All" };
                var uniqueSections = new System.Collections.Generic.HashSet<string> { "All" }; 
                var uniqueStatuses = new System.Collections.Generic.HashSet<string> { "All" };

                foreach (var s in allStudents)
                {
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
            finally
            {
                IsProcessing = false; // Hide the spinner
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
            IsProcessing = true;
            try 
            {
                var db = new DatabaseService().GetConnection();
                var selectedStudents = _allAvailableStudents.Where(s => s.IsSelected).ToList();

                // 1. Create a list in memory
                var newRosters = selectedStudents.Select(student => new ClassRoster
                {
                    ClassID = ClassId, 
                    StudentID = student.DbModel.StudentID 
                }).ToList();

                // 2. BULK INSERT: This hits the hard drive exactly 1 time instead of 50+ times.
                await db.InsertAllAsync(newRosters);

                IsEnrolling = false;
                
                // Use your existing helper to refresh all data seamlessly
                await RefreshRostersAsync(); 
                
                int count = selectedStudents.Count;
                ShowToastMessage?.Invoke(count == 1 
                    ? $"Successfully enrolled {selectedStudents[0].DbModel.FirstName} {selectedStudents[0].DbModel.LastName}." 
                    : $"Successfully enrolled {count} students.");
            }
            finally { IsProcessing = false; }
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
            IsProcessing = true;
            try 
            {
                if (!_studentsToRemove.Any()) return;
                var db = new DatabaseService().GetConnection();
                
                var classAssessments = await db.Table<Assessment>().Where(a => a.ClassID == ClassId).ToListAsync();
                var assessmentIds = classAssessments.Select(a => a.AssessmentID).ToList();
                var studentIds = _studentsToRemove.Select(s => s.StudentID).ToList();

                // 1. Fetch records safely using the ORM (Translates to a safe SQL 'IN' clause automatically)
                var rostersToDelete = await db.Table<ClassRoster>()
                    .Where(r => r.ClassID == ClassId && studentIds.Contains(r.StudentID))
                    .ToListAsync();

                var groupMembersToDelete = new System.Collections.Generic.List<AssessmentGroupMember>();
                if (assessmentIds.Any())
                {
                    groupMembersToDelete = await db.Table<AssessmentGroupMember>()
                        .Where(m => studentIds.Contains(m.StudentID) && assessmentIds.Contains(m.AssessmentID))
                        .ToListAsync();
                }

                // 2. Perform a single Bulk Transaction
                await db.RunInTransactionAsync(tran => 
                {
                    foreach (var r in rostersToDelete) tran.Delete(r);
                    foreach (var m in groupMembersToDelete) tran.Delete(m);
                });
                
                await RefreshRostersAsync(); 
                
                int count = _studentsToRemove.Count;
                ShowToastMessage?.Invoke(count == 1 
                    ? $"Successfully unenrolled {_studentsToRemove[0].FirstName} {_studentsToRemove[0].LastName}." 
                    : $"Successfully unenrolled {count} students.");
                    
                CancelRemoveStudent(); 
            }
            catch (System.Exception ex)
            {
                ShowToastMessage?.Invoke($"Error unenrolling students: {ex.Message}");
            }
            finally { IsProcessing = false; }
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
            IsProcessing = true;
            try 
            {
                if (!_studentsToTransfer.Any() || SelectedTransferClass == null) return;
                var db = new DatabaseService().GetConnection();
                
                var classAssessments = await db.Table<Assessment>().Where(a => a.ClassID == ClassId).ToListAsync();
                var assessmentIds = classAssessments.Select(a => a.AssessmentID).ToList();
                var studentIds = _studentsToTransfer.Select(s => s.StudentID).ToList();

                // 1. Fetch records safely using the ORM
                var rostersToUpdate = await db.Table<ClassRoster>()
                    .Where(r => r.ClassID == ClassId && studentIds.Contains(r.StudentID))
                    .ToListAsync();

                var groupMembersToDelete = new System.Collections.Generic.List<AssessmentGroupMember>();
                if (assessmentIds.Any())
                {
                    groupMembersToDelete = await db.Table<AssessmentGroupMember>()
                        .Where(m => studentIds.Contains(m.StudentID) && assessmentIds.Contains(m.AssessmentID))
                        .ToListAsync();
                }

                // 2. Perform a single Bulk Transaction
                await db.RunInTransactionAsync(tran => 
                {
                    // Update the ClassID for all selected rosters
                    foreach (var r in rostersToUpdate) 
                    {
                        r.ClassID = SelectedTransferClass.ClassID;
                        tran.Update(r);
                    }
                    
                    // Erase their group memberships from the old class
                    foreach (var m in groupMembersToDelete) tran.Delete(m);
                });

                await RefreshRostersAsync(); 
                
                int count = _studentsToTransfer.Count;
                ShowToastMessage?.Invoke(count == 1 
                    ? $"Successfully transferred {_studentsToTransfer[0].FirstName} {_studentsToTransfer[0].LastName}." 
                    : $"Successfully transferred {count} students.");
                    
                CancelTransferStudent();
            }
            catch (System.Exception ex)
            {
                ShowToastMessage?.Invoke($"Error transferring students: {ex.Message}");
            }
            finally { IsProcessing = false; }
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