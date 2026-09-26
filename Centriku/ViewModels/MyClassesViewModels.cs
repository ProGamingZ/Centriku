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
        private readonly Action<ViewModelBase> _navigateAction;
        public System.Action<string>? ShowToastMessage { get; set; }
        [ObservableProperty] public partial bool IsProcessing { get; set; } = false;
        [ObservableProperty] public partial bool IsDeleteModalOpen { get; set; } = false;
        [ObservableProperty] public partial string DeleteConfirmationMessage { get; set; } = string.Empty;
        private ClassCardViewModel? _classToDelete;
        [ObservableProperty] public partial ObservableCollection<ClassCardViewModel> ActiveClasses { get; set; } = new();
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
        public IRelayCommand ConfirmDeleteCommand { get; }
        public IRelayCommand CancelDeleteCommand { get; }
        public IRelayCommand<ClassCardViewModel> OpenClassCommand { get; }


        public MyClassesViewModel(Action<ViewModelBase> navigateAction)
        {
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
            var db = new DatabaseService().GetConnection();
            await db.CreateTableAsync<GradingTemplate>();
            var templates = await db.Table<GradingTemplate>().ToListAsync();
            AvailableTemplates = new ObservableCollection<GradingTemplate>(templates);
        }

        private async Task LoadClasses()
        {
            var db = new DatabaseService().GetConnection();
            await db.CreateTableAsync<TeacherClass>();
            _allClasses = await db.Table<TeacherClass>().ToListAsync();
            
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
            foreach (var c in filtered)
            {
                var templateName = AvailableTemplates.FirstOrDefault(t => t.TemplateID == c.GradingTemplateID)?.TemplateName ?? "Unknown Template";
                ActiveClasses.Add(new ClassCardViewModel(c, templateName));
            }
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
                var db = new DatabaseService().GetConnection();
                bool isUpdate = _editingClassId.HasValue;

                if (isUpdate)
                {
                    // UPDATE
                    var classToUpdate = await db.Table<TeacherClass>().Where(c => c.ClassID == _editingClassId.Value).FirstOrDefaultAsync();
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
            
            IsProcessing = true; // Show spinner
            
            try
            {
                var db = new DatabaseService().GetConnection();
                
                // Optional: You may want to add raw SQL here to cascade delete Assessment columns and Scores tied to this ClassID
                
                await db.DeleteAsync(_classToDelete.DbModel);
                ShowToastMessage?.Invoke($"Deleted class: '{_classToDelete.SubjectName}'");
                await LoadClasses();
            }
            catch (Exception ex)
            {
                ShowToastMessage?.Invoke($"Error deleting class: {ex.Message}");
            }
            finally
            {
                IsProcessing = false; // Hide spinner
                CancelDelete();       // Close modal
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
                    var newGradebook = new GradebookViewModel();
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
        
        public async Task RefreshDataAsync()
        {
            await LoadTemplates();
            await LoadClasses();
        }
    }

    public partial class ClassCardViewModel : ObservableObject
    {
        public TeacherClass DbModel { get; }
        public string TemplateName { get; }

        public string SubjectName => DbModel.SubjectName ?? string.Empty;
        public string SectionLabel => DbModel.SectionLabel ?? string.Empty;
        public string Program => DbModel.Program ?? string.Empty;
        public string ProgramSectionDisplay => $"{Program}_{SectionLabel}";
        public string ProfessorName => DbModel.ProfessorName ?? string.Empty;
        public string AcademicYear => DbModel.AcademicYear ?? string.Empty;
        public string Term => DbModel.Term ?? string.Empty;
        public ClassCardViewModel(TeacherClass teacherClass, string templateName)
        {
            DbModel = teacherClass;
            TemplateName = templateName;
        }
    }
}