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
        [ObservableProperty] public partial ObservableCollection<ClassCardViewModel> ActiveClasses { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<GradingTemplate> AvailableTemplates { get; set; } = new();
        [ObservableProperty] public partial bool IsAddingClass { get; set; } = false;
        [ObservableProperty]  public partial string NewSubjectName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewSectionLabel { get; set; } = string.Empty;   
        [ObservableProperty] public partial string NewAcademicYear { get; set; } = "2025-2026";        
        [ObservableProperty] public partial ObservableCollection<string> AvailableTerms { get; set; } = [];
        [ObservableProperty] public partial string NewTerm { get; set; } = string.Empty;     
        [ObservableProperty] public partial string NewEducationMode { get; set; } = "Quarterly";
        public ObservableCollection<string> AvailableEducationModes { get; } = ["Quarterly", "Semestral"];
        [ObservableProperty] public partial GradingTemplate? SelectedTemplate { get; set; }

        
        private int? _editingClassId = null;
        private static readonly System.Collections.Generic.Dictionary<int, GradebookViewModel> _gradebookCache = [];

        public IRelayCommand ToggleAddClassFormCommand { get; }
        public IRelayCommand SaveClassCommand { get; }
        public IRelayCommand<ClassCardViewModel> EditClassCommand { get; } 
        public IRelayCommand<ClassCardViewModel> DeleteClassCommand { get; }
        public IRelayCommand<ClassCardViewModel> OpenClassCommand { get; }

        partial void OnNewEducationModeChanged(string value)
        {
            UpdateAvailableTerms(value);
        }
        private void UpdateAvailableTerms(string mode)
        {
            AvailableTerms.Clear();

            if (mode == "Semestral")
            {
                AvailableTerms.Add("1st Sem");
                AvailableTerms.Add("2nd Sem");
                NewTerm = "1st Sem"; 
            }
            else // Quarterly
            {
                AvailableTerms.Add("Q1-Q4");
                NewTerm = "Q1-Q4"; 
            }
        }

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
            DeleteClassCommand = new RelayCommand<ClassCardViewModel>(DeleteClass!);
            OpenClassCommand = new RelayCommand<ClassCardViewModel>(OpenClass!);

            DirectoryViewModel.OnStudentRosterChanged += () =>
            {
                foreach (var cachedGradebook in _gradebookCache.Values)
                {
                    _ = cachedGradebook.RefreshRostersAsync();
                }
            };
            UpdateAvailableTerms(NewEducationMode);
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
            var classes = await db.Table<TeacherClass>().ToListAsync();
            ActiveClasses.Clear();
            foreach (var c in classes)
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
            NewAcademicYear = classCard.AcademicYear;
            NewTerm = classCard.Term;
            NewEducationMode = classCard.DbModel.EducationMode ?? "Quarterly";
            SelectedTemplate = AvailableTemplates.FirstOrDefault(t => t.TemplateID == classCard.DbModel.GradingTemplateID);
            
            IsAddingClass = true;
        }

        private async void SaveClass()
        {
            if (string.IsNullOrWhiteSpace(NewSubjectName) || SelectedTemplate == null) return;
            var db = new DatabaseService().GetConnection();

            if (_editingClassId.HasValue)
            {
                // UPDATE
                var classToUpdate = await db.Table<TeacherClass>().Where(c => c.ClassID == _editingClassId.Value).FirstOrDefaultAsync();
                classToUpdate.SubjectName = NewSubjectName;
                classToUpdate.SectionLabel = NewSectionLabel;
                classToUpdate.AcademicYear = NewAcademicYear;
                classToUpdate.Term = NewTerm;
                classToUpdate.EducationMode = NewEducationMode;
                classToUpdate.GradingTemplateID = SelectedTemplate.TemplateID;
                
                await db.UpdateAsync(classToUpdate);
            }
            else
            {
                // CREATE
                var newClass = new TeacherClass
                {
                    SubjectName = NewSubjectName,
                    SectionLabel = NewSectionLabel,
                    AcademicYear = NewAcademicYear,
                    Term = NewTerm,
                    EducationMode = NewEducationMode,
                    GradingTemplateID = SelectedTemplate.TemplateID
                };
                await db.InsertAsync(newClass);
            }

            ResetForm();
            await LoadClasses();
        }
        private async void DeleteClass(ClassCardViewModel classCard)
        {
            if (classCard == null) return;
            var db = new DatabaseService().GetConnection();
            
            await db.DeleteAsync(classCard.DbModel);
            await LoadClasses();
        }
        private void ResetForm()
        {
            _editingClassId = null;
            NewSubjectName = string.Empty;
            NewSectionLabel = string.Empty;
            SelectedTemplate = null;
            IsAddingClass = false;
            NewEducationMode = "Quarterly";
            UpdateAvailableTerms("Quarterly");
        }

       
        private void OpenClass(ClassCardViewModel selectedClass)
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
        public string AcademicYear => DbModel.AcademicYear ?? string.Empty;
        public string Term => DbModel.Term ?? string.Empty;
        public string EducationMode => DbModel.EducationMode ?? "Quarterly";
        public ClassCardViewModel(TeacherClass teacherClass, string templateName)
        {
            DbModel = teacherClass;
            TemplateName = templateName;
        }
    }
}