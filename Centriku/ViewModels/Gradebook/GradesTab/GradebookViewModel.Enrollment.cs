using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;
using Centriku.ViewModels.Modals;

namespace Centriku.ViewModels
{
    public partial class GradebookViewModel
    {   
        [ObservableProperty] public partial bool IsEnrolling { get; set; } = false;
        [ObservableProperty] public partial bool IsRemoveStudentModalOpen { get; set; } = false;
        [ObservableProperty] public partial string RemoveModalMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsTransferStudentModalOpen { get; set; } = false;
        [ObservableProperty] public partial string TransferModalMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial ObservableCollection<TeacherClass> AvailableTransferClasses { get; set; } = new();
        [ObservableProperty] public partial TeacherClass? SelectedTransferClass { get; set; }
        
        private System.Collections.Generic.List<EnrollmentItemViewModel> _allAvailableStudents = new();
        [ObservableProperty] public partial ObservableCollection<EnrollmentItemViewModel> AvailableStudents { get; set; } = new();
        [ObservableProperty] public partial string DynamicEnrollmentCounter { get; set; } = "Showing: 0 students";
        
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
                IsEnrolling = false;
                return;
            }
            
            IsEnrolling = true;
            IsAddingAssessment = false;
            IsProcessing = true;

            try
            {
                await Task.Delay(50); 

                var db = _dbService.GetConnection();
                var allStudents = await db.Table<Student>().ToListAsync();
                
                var enrolledIds = new System.Collections.Generic.HashSet<string>(_allGradebookRows.Select(s => s.StudentID));

                _allAvailableStudents.Clear();
                var uniqueYears = new System.Collections.Generic.HashSet<string> { "All" };
                var uniquePrograms = new System.Collections.Generic.HashSet<string> { "All" };
                var uniqueSections = new System.Collections.Generic.HashSet<string> { "All" }; 
                var uniqueStatuses = new System.Collections.Generic.HashSet<string> { "All" };

                foreach (var s in allStudents.OrderBy(st => st.LastName))
                {
                    if (s.StudentID != null && !enrolledIds.Contains(s.StudentID) && !s.IsArchived && s.EnrollmentStatus != "Dropped")
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
                IsProcessing = false;
            }
        }

        private void FilterAvailableStudents()
        {
            // STICKY SELECTION: Keep checked students visible even when switching Section/Program filters!
            var filtered = _allAvailableStudents.Where(s => 
                s.IsSelected ||
                ((SelectedEnrollmentYear == "All" || s.DbModel.GradeYearLevel == SelectedEnrollmentYear) &&
                 (SelectedEnrollmentProgram == "All" || s.DbModel.Program == SelectedEnrollmentProgram) &&
                 (SelectedEnrollmentSection == "All" || s.DbModel.SectionName == SelectedEnrollmentSection) && 
                 (SelectedEnrollmentStatus == "All" || s.DbModel.EnrollmentStatus == SelectedEnrollmentStatus))
            ).ToList();

            AvailableStudents = new ObservableCollection<EnrollmentItemViewModel>(filtered);
            DynamicEnrollmentCounter = $"Showing: {filtered.Count} available";
        }

        private async void SaveEnrollment()
        {
            var selectedStudents = _allAvailableStudents.Where(s => s.IsSelected).ToList();
            if (!selectedStudents.Any())
            {
                ShowToastMessage?.Invoke("Please select at least one student to enroll.");
                return;
            }

            IsProcessing = true;
            try 
            {
                var db = _dbService.GetConnection();

                var newRosters = selectedStudents.Select(student => new ClassRoster
                {
                    ClassID = ClassId, 
                    StudentID = student.DbModel.StudentID 
                }).ToList();

                await db.InsertAllAsync(newRosters, runInTransaction: false);

                IsEnrolling = false;
                await RefreshRostersAsync(); 
                
                int count = selectedStudents.Count;
                ShowToastMessage?.Invoke(count == 1 
                    ? $"Successfully enrolled {selectedStudents[0].DbModel.FirstName} {selectedStudents[0].DbModel.LastName}." 
                    : $"Successfully enrolled {count} students.");
            }
            finally { IsProcessing = false; }
        }

        [ObservableProperty] public partial RemoveStudentModalViewModel? RemoveStudentModal { get; set; }
        [ObservableProperty] public partial TransferStudentModalViewModel? TransferStudentModal { get; set; }

        [RelayCommand]
        public void BulkRemoveStudents()
        {
            var studentsToRemove = _allGradebookRows.Where(r => r.IsSelected).Select(r => r.StudentInfo).ToList();
            if (!studentsToRemove.Any()) { ShowToastMessage?.Invoke("Please select at least one student first."); return; }
            
            RemoveStudentModal = new RemoveStudentModalViewModel(
                _dbService,
                ClassId, 
                studentsToRemove, 
                RefreshRostersAsync, 
                () => { IsRemoveStudentModalOpen = false; RemoveStudentModal = null; IsAllRosterSelected = false; }, 
                ShowToastMessage!);
                
            IsRemoveStudentModalOpen = true;
        }
        
        [RelayCommand]
        public async Task BulkTransferStudents()
        {
            var studentsToTransfer = _allGradebookRows.Where(r => r.IsSelected).Select(r => r.StudentInfo).ToList();
            if (!studentsToTransfer.Any()) { ShowToastMessage?.Invoke("Please select at least one student first."); return; }
            
            TransferStudentModal = new TransferStudentModalViewModel(
                _dbService,
                ClassId, 
                ClassTitle,
                studentsToTransfer, 
                RefreshRostersAsync, 
                () => { IsTransferStudentModalOpen = false; TransferStudentModal = null; IsAllRosterSelected = false; }, 
                ShowToastMessage!);
                
            await TransferStudentModal.LoadClassesAsync();
            IsTransferStudentModalOpen = true;
        }
    }
}