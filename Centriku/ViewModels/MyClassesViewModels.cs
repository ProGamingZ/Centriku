using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels
{
    public partial class MyClassesViewModel : ViewModelBase
    {
        private readonly IDatabaseService _dbService;
        private readonly Action<ViewModelBase> _navigateAction;
        public System.Action<string>? ShowToastMessage { get; set; }
        [ObservableProperty] public partial bool IsProcessing { get; set; } = false;
        [ObservableProperty] public partial bool IsDeleteModalOpen { get; set; } = false;
        [ObservableProperty] public partial string DeleteConfirmationMessage { get; set; } = string.Empty;
        private ClassCardViewModel? _classToDelete;
        [ObservableProperty] public partial bool IsDuplicateModalOpen { get; set; } = false;
        [ObservableProperty] public partial string DuplicateConfirmationMessage { get; set; } = string.Empty;
        private ClassCardViewModel? _classToDuplicate;
        [ObservableProperty] public partial ObservableCollection<ClassCardViewModel> ActiveClasses { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<ClassCardViewModel> InactiveClasses { get; set; } = new();
        [ObservableProperty] public partial string ActiveClassesHeader { get; set; } = "Active Classes (0)";
        [ObservableProperty] public partial string InactiveClassesHeader { get; set; } = "Archived Classes (0)";
        private System.Collections.Generic.List<ClassRoster> _allRosters = new();
        [ObservableProperty] public partial ObservableCollection<GradingTemplate> AvailableTemplates { get; set; } = new();
        [ObservableProperty] public partial bool IsAddingClass { get; set; } = false;

        private System.Collections.Generic.List<TeacherClass> _allClasses = new();
        [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;
        [ObservableProperty] public partial ObservableCollection<string> AvailableYearFilters { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<string> AvailableTermFilters { get; set; } = new();
        [ObservableProperty] public partial string SelectedYearFilter { get; set; } = "All Years";
        [ObservableProperty] public partial string SelectedTermFilter { get; set; } = "All Terms";

        partial void OnSearchQueryChanged(string value) => FilterClasses();
        partial void OnSelectedYearFilterChanged(string value) => FilterClasses();
        partial void OnSelectedTermFilterChanged(string value) => FilterClasses();
        
        [ObservableProperty] public partial string NewSubjectName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewSectionLabel { get; set; } = string.Empty;   
        [ObservableProperty] public partial string NewProgram { get; set; } = string.Empty;   
        [ObservableProperty] public partial string NewProfessorName { get; set; } = string.Empty;   
        [ObservableProperty] public partial string NewAcademicYear { get; set; } = "2025-2026";        
        [ObservableProperty] public partial ObservableCollection<string> AvailableTerms { get; set; } = [];
        [ObservableProperty] public partial string NewTerm { get; set; } = string.Empty;     
        [ObservableProperty] public partial GradingTemplate? SelectedTemplate { get; set; }
        
        private int? _editingClassId = null;
        private static readonly System.Collections.Generic.Dictionary<int, GradebookViewModel> _gradebookCache = [];

        public IRelayCommand ToggleAddClassFormCommand { get; }
        public IRelayCommand SaveClassCommand { get; }
        public IRelayCommand<ClassCardViewModel> EditClassCommand { get; } 
        public IRelayCommand<ClassCardViewModel> DeleteClassCommand { get; }
        public IAsyncRelayCommand ConfirmDeleteCommand { get; }
        public IRelayCommand CancelDeleteCommand { get; }
        public IRelayCommand<ClassCardViewModel> OpenClassCommand { get; }
        public IAsyncRelayCommand<ClassCardViewModel> ToggleClassStateCommand { get; }
        public IRelayCommand<ClassCardViewModel> DuplicateClassCommand { get; } 
        public IAsyncRelayCommand ConfirmDuplicateCommand { get; }
        public IRelayCommand CancelDuplicateCommand { get; }


        public MyClassesViewModel(IDatabaseService dbService,Action<ViewModelBase> navigateAction)
        {
            _dbService = dbService;
            _navigateAction = navigateAction;

            ToggleAddClassFormCommand = new RelayCommand(() => 
            {
                if (IsAddingClass) ResetForm(); 
                else IsAddingClass = true;      
            });
            
            SaveClassCommand = new RelayCommand(SaveClass);
            EditClassCommand = new RelayCommand<ClassCardViewModel>(EditClass!);
            DeleteClassCommand = new RelayCommand<ClassCardViewModel>(PromptDeleteClass!);
            ConfirmDeleteCommand = new AsyncRelayCommand(ConfirmDeleteAsync);
            CancelDeleteCommand = new RelayCommand(CancelDelete);
            OpenClassCommand = new RelayCommand<ClassCardViewModel>(OpenClass!);
            ToggleClassStateCommand = new AsyncRelayCommand<ClassCardViewModel>(ToggleClassStateAsync!);
            DuplicateClassCommand = new RelayCommand<ClassCardViewModel>(PromptDuplicateClass!);
            ConfirmDuplicateCommand = new AsyncRelayCommand(ConfirmDuplicateAsync);
            CancelDuplicateCommand = new RelayCommand(CancelDuplicate);

            DirectoryViewModel.OnStudentRosterChanged += () =>
            {
                foreach (var cachedGradebook in _gradebookCache.Values)
                {
                    _ = cachedGradebook.RefreshRostersAsync();
                }
            };
            AvailableTerms = ["1st Sem", "2nd Sem", "Midyear/Summer"];
            NewTerm = "1st Sem";
        }

        private async void InitializeData()
        {
            await LoadTemplates();
            await LoadClasses();
        }

        private async Task LoadTemplates()
        {
            var db = _dbService.GetConnection();
            await db.CreateTableAsync<GradingTemplate>();
            var templates = await db.Table<GradingTemplate>().ToListAsync();
            AvailableTemplates = new ObservableCollection<GradingTemplate>(templates);
        }

        private async Task LoadClasses()
        {
            var db = _dbService.GetConnection();
            await db.CreateTableAsync<TeacherClass>();
            _allClasses = await db.Table<TeacherClass>().ToListAsync();
            _allRosters = await db.Table<ClassRoster>().ToListAsync();
            
            // Populate Dynamic Filter Dropdowns
            var years = _allClasses.Select(c => c.AcademicYear).Where(y => !string.IsNullOrWhiteSpace(y)).Distinct().OrderByDescending(y => y).ToList();
            AvailableYearFilters.Clear(); AvailableYearFilters.Add("All Years");
            foreach (var y in years) AvailableYearFilters.Add(y!);
            if (!AvailableYearFilters.Contains(SelectedYearFilter)) SelectedYearFilter = "All Years";

            var terms = _allClasses.Select(c => c.Term).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().OrderBy(t => t).ToList();
            AvailableTermFilters.Clear(); AvailableTermFilters.Add("All Terms");
            foreach (var t in terms) AvailableTermFilters.Add(t!);
            if (!AvailableTermFilters.Contains(SelectedTermFilter)) SelectedTermFilter = "All Terms";

            FilterClasses();
        }

        private void FilterClasses()
        {
            var filtered = _allClasses.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLower();
                filtered = filtered.Where(c => 
                    c.SubjectName?.ToLower().Contains(q) == true || 
                    c.SectionLabel?.ToLower().Contains(q) == true ||
                    c.Program?.ToLower().Contains(q) == true);
            }

            if (SelectedYearFilter != "All Years") filtered = filtered.Where(c => c.AcademicYear == SelectedYearFilter);
            if (SelectedTermFilter != "All Terms") filtered = filtered.Where(c => c.Term == SelectedTermFilter);

            ActiveClasses.Clear();
            InactiveClasses.Clear();
            
            foreach (var c in filtered)
            {
                var templateName = AvailableTemplates.FirstOrDefault(t => t.TemplateID == c.GradingTemplateID)?.TemplateName ?? "Unknown Template";
                
                // NEW: Count the students dynamically
                int studentCount = _allRosters.Count(r => r.ClassID == c.ClassID);
                var newCard = new ClassCardViewModel(c, templateName, studentCount);
                
                if (c.IsActive) ActiveClasses.Add(newCard);
                else InactiveClasses.Add(newCard);
            }

            // Update the UI Headers dynamically based on the current lists
            ActiveClassesHeader = $"Active Classes ({ActiveClasses.Count})";
            InactiveClassesHeader = $"Archived Classes ({InactiveClasses.Count})";
        }

        private void EditClass(ClassCardViewModel classCard)
        {
            if (classCard == null) return;
            _editingClassId = classCard.DbModel.ClassID;
            NewSubjectName = classCard.SubjectName;
            NewSectionLabel = classCard.SectionLabel;
            NewProgram = classCard.Program;
            NewProfessorName = classCard.ProfessorName;
            NewAcademicYear = classCard.AcademicYear;
            NewTerm = classCard.Term;
            SelectedTemplate = AvailableTemplates.FirstOrDefault(t => t.TemplateID == classCard.DbModel.GradingTemplateID);
            
            IsAddingClass = true;
        }

        private async void SaveClass()
        {
            if (string.IsNullOrWhiteSpace(NewSubjectName) || SelectedTemplate == null) return;
            
            IsProcessing = true; // Show loading spinner
            
            try
            {
                var db = _dbService.GetConnection();

                if (_editingClassId is int editingClassId)
                {
                    // UPDATE

                    var classToUpdate = await db.Table<TeacherClass>()
                        .Where(c => c.ClassID == editingClassId)
                        .FirstOrDefaultAsync();

                    if (classToUpdate is null)
                    {
                        ShowToastMessage?.Invoke("Class not found. Refresh the class list and try again.");
                        return;
                    }

                    classToUpdate.SubjectName = NewSubjectName;
                    classToUpdate.SectionLabel = NewSectionLabel;
                    classToUpdate.Program = NewProgram;
                    classToUpdate.ProfessorName = NewProfessorName;
                    classToUpdate.AcademicYear = NewAcademicYear;
                    classToUpdate.Term = NewTerm;
                    classToUpdate.GradingTemplateID = SelectedTemplate.TemplateID;
                    
                    await db.UpdateAsync(classToUpdate);
                    ShowToastMessage?.Invoke($"Class '{NewSubjectName}' updated successfully.");
                }
                else
                {
                    // CREATE
                    var newClass = new TeacherClass
                    {
                        SubjectName = NewSubjectName,
                        SectionLabel = NewSectionLabel,
                        Program = NewProgram,
                        ProfessorName = NewProfessorName,
                        AcademicYear = NewAcademicYear,
                        Term = NewTerm,
                        GradingTemplateID = SelectedTemplate.TemplateID
                    };
                    await db.InsertAsync(newClass);
                    ShowToastMessage?.Invoke($"Class '{NewSubjectName}' created successfully.");
                }

                ResetForm();
                await LoadClasses();
            }
            catch (Exception ex)
            {
                ShowToastMessage?.Invoke($"Error saving class: {ex.Message}");
            }
            finally
            {
                IsProcessing = false; // Hide spinner
            }
        }
        private void PromptDeleteClass(ClassCardViewModel classCard)
        {
            if (classCard == null) return;
            _classToDelete = classCard;
            DeleteConfirmationMessage = $"Are you sure you want to delete '{classCard.SubjectName} - {classCard.SectionLabel}'?\n\nThis will permanently erase the class record. Student grades and records tied to this class will be lost.";
            IsDeleteModalOpen = true;
        }

        private void CancelDelete()
        {
            IsDeleteModalOpen = false;
            _classToDelete = null;
        }
        private async Task ConfirmDeleteAsync()
        {
            if (_classToDelete == null) return;
            
            IsProcessing = true; 
            
            try
            {
                var db = _dbService.GetConnection();
                int classId = _classToDelete.DbModel.ClassID;
                
                // 1. Fetch direct child records
                var assessments = await db.Table<Assessment>().Where(a => a.ClassID == classId).ToListAsync();
                var rosters = await db.Table<ClassRoster>().Where(r => r.ClassID == classId).ToListAsync();
                var attendance = await db.Table<AttendanceRecord>().Where(r => r.ClassID == classId).ToListAsync();
                var recitations = await db.Table<RecitationQuestion>().Where(r => r.ClassID == classId).ToListAsync();

                // 2. Fetch grandchild records (Scores and Groups tied to the Assessments)
                var assessmentIds = assessments.Select(a => a.AssessmentID).ToList();
                
                var scoresToDelete = new System.Collections.Generic.List<Score>();
                var groupsToDelete = new System.Collections.Generic.List<AssessmentGroup>();
                var membersToDelete = new System.Collections.Generic.List<AssessmentGroupMember>();

                if (assessmentIds.Any())
                {
                    // Fetch all and filter in memory to bypass SQLite's .Contains() query limitations
                    var allScores = await db.Table<Score>().ToListAsync();
                    scoresToDelete = allScores.Where(s => assessmentIds.Contains(s.AssessmentID)).ToList();
                    
                    var allGroups = await db.Table<AssessmentGroup>().ToListAsync();
                    groupsToDelete = allGroups.Where(g => assessmentIds.Contains(g.AssessmentID)).ToList();
                    
                    var allMembers = await db.Table<AssessmentGroupMember>().ToListAsync();
                    membersToDelete = allMembers.Where(m => assessmentIds.Contains(m.AssessmentID)).ToList();
                }

                // 3. SECURE TRANSACTION: Delete everything using the ORM safely
                await db.RunInTransactionAsync(tran => 
                {
                    foreach (var s in scoresToDelete) tran.Delete(s);
                    foreach (var g in groupsToDelete) tran.Delete(g);
                    foreach (var m in membersToDelete) tran.Delete(m);
                    
                    foreach (var a in assessments) tran.Delete(a);
                    foreach (var r in rosters) tran.Delete(r);
                    foreach (var att in attendance) tran.Delete(att);
                    foreach (var req in recitations) tran.Delete(req);
                    
                    // Finally, delete the class itself
                    tran.Delete(_classToDelete.DbModel);
                });

                // Evict the deleted class from the RAM cache
                _gradebookCache.Remove(classId);

                ShowToastMessage?.Invoke($"Deleted class: '{_classToDelete.SubjectName}'");
                await LoadClasses();
            }
            catch (Exception ex)
            {
                ShowToastMessage?.Invoke($"Error deleting class: {ex.Message}");
            }
            finally
            {
                IsProcessing = false; 
                CancelDelete();       
            }
        }

        private void ResetForm()
        {
            _editingClassId = null;
            NewSubjectName = string.Empty;
            NewSectionLabel = string.Empty;
            NewProgram = string.Empty;
            NewProfessorName = string.Empty;
            SelectedTemplate = null;
            IsAddingClass = false;
        }

        private async void OpenClass(ClassCardViewModel selectedClass)
        {
            if (selectedClass != null)
            {
                int classId = selectedClass.DbModel.ClassID;

                if (!_gradebookCache.TryGetValue(classId, out GradebookViewModel? value))
                {
                    var newGradebook = new GradebookViewModel(_dbService);
                    newGradebook.Initialize(classId, selectedClass.SubjectName);
                    _gradebookCache[classId] = newGradebook;
                }
                else
                {
                    await value.RefreshRostersAsync(); 
                    value.RecalculateFinalGrades();
                }
                
                _navigateAction(_gradebookCache[classId]);
            }
        }
        private async Task ToggleClassStateAsync(ClassCardViewModel classCard)
        {
            if (classCard == null) return;
            IsProcessing = true; 
            
            try
            {
                var db = _dbService.GetConnection();
                classCard.DbModel.IsActive = !classCard.DbModel.IsActive; // Flip the state
                await db.UpdateAsync(classCard.DbModel);
                
                string toastMsg = classCard.DbModel.IsActive ? $"Restored '{classCard.SubjectName}' to Active Status." : $"Archived '{classCard.SubjectName}'.";
                ShowToastMessage?.Invoke(toastMsg);
                
                await LoadClasses(); // Refresh UI
            }
            catch (Exception ex)
            {
                ShowToastMessage?.Invoke($"Error toggling class state: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }
        private void PromptDuplicateClass(ClassCardViewModel classCard)
        {
            if (classCard == null) return;
            _classToDuplicate = classCard;
            DuplicateConfirmationMessage = $"Are you sure you want to duplicate '{classCard.SubjectName} - {classCard.SectionLabel}'?\n\nThis will create a new class and copy all enrolled students. Assessments, scores, and attendance will NOT be included.";
            IsDuplicateModalOpen = true;
        }

        private void CancelDuplicate()
        {
            IsDuplicateModalOpen = false;
            _classToDuplicate = null;
        }

        private async Task ConfirmDuplicateAsync()
        {
            if (_classToDuplicate == null) return;
            IsProcessing = true;
            
            try
            {
                var db = _dbService.GetConnection();
                var original = _classToDuplicate.DbModel;
                
                // 1. Clone the TeacherClass
                var newClass = new TeacherClass
                {
                    SubjectName = original.SubjectName + " (Copy)",
                    SectionLabel = original.SectionLabel,
                    Program = original.Program,
                    ProfessorName = original.ProfessorName,
                    AcademicYear = original.AcademicYear,
                    Term = original.Term,
                    GradingTemplateID = original.GradingTemplateID,
                    IsActive = true
                };
                
                await db.InsertAsync(newClass);
                
                // 2. Clone the Enrolled Students (ClassRoster) ONLY
                var oldRosters = await db.Table<ClassRoster>().Where(r => r.ClassID == original.ClassID).ToListAsync();
                var newRosters = oldRosters.Select(r => new ClassRoster 
                { 
                    ClassID = newClass.ClassID, 
                    StudentID = r.StudentID,
                    HasRecited = false // Reset recitation state for the new class
                }).ToList();
                
                if (newRosters.Any()) await db.InsertAllAsync(newRosters);
                
                ShowToastMessage?.Invoke($"Successfully duplicated '{original.SubjectName}'.");
                await LoadClasses();
            }
            catch (Exception ex)
            {
                ShowToastMessage?.Invoke($"Error duplicating class: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                CancelDuplicate();
            }
        }
        
        public async Task RefreshDataAsync()
        {
            await LoadTemplates();
            await LoadClasses();
        }
    }

    public partial class ClassCardViewModel(TeacherClass teacherClass, string templateName, int studentCount) : ObservableObject
    {
        public TeacherClass DbModel { get; } = teacherClass;
        public string TemplateName { get; } = templateName;
        public string StudentCountDisplay { get; } = $"{studentCount} Student{(studentCount == 1 ? "" : "s")}";

        public string SubjectName => DbModel.SubjectName ?? string.Empty;
        public string SectionLabel => DbModel.SectionLabel ?? string.Empty;
        public string Program => DbModel.Program ?? string.Empty;
        public string ProgramSectionDisplay => $"{Program}_{SectionLabel}";
        public string ProfessorName => DbModel.ProfessorName ?? string.Empty;
        public string AcademicYear => DbModel.AcademicYear ?? string.Empty;
        public string Term => DbModel.Term ?? string.Empty;
        public bool IsActive => DbModel.IsActive;
    }
}