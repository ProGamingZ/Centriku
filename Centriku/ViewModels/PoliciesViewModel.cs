using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels
{
    public partial class PoliciesViewModel : ViewModelBase
    {
        // --- 1. UI STATE TRACKERS ---
        [ObservableProperty] public partial string EditorTitleText { get; set; } = "Create New Template";
        [ObservableProperty] public partial string SaveButtonText { get; set; } = "Create Template";
        [ObservableProperty] public partial bool IsEditing { get; set; } = false;
        private int? _editingTemplateId = null;

        // --- 2. TEMPLATE DATA ---
        [ObservableProperty] public partial ObservableCollection<PolicyCategoryItem> Categories { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<Centriku.Models.GradingTemplate> SavedTemplates { get; set; } = new();
        [ObservableProperty] public partial string TemplateName { get; set; } = "New Grading Template";
        [ObservableProperty] public partial decimal? PassingGrade { get; set; } = 75m;
        [ObservableProperty] public partial decimal TotalWeight { get; set; }
        [ObservableProperty] public partial bool IsValidPolicy { get; set; }

        // --- 3. CALCULATION MODES (Plain Language Mapping) ---
        public ObservableCollection<string> AvailableCalculationModes { get; } = [
            "Formula-Based Scale (NRFG)", 
            "Boundary & Tier Scale (CRG)"
        ];
        [ObservableProperty] public partial string CalculationMode { get; set; } = "Formula-Based Scale (NRFG)";
        
        public bool IsBoundaryMode => CalculationMode == "Boundary & Tier Scale (CRG)";
        public bool IsFormulaMode => CalculationMode == "Formula-Based Scale (NRFG)"; 
        
        partial void OnCalculationModeChanged(string value) 
        { 
            OnPropertyChanged(nameof(IsBoundaryMode)); 
            OnPropertyChanged(nameof(IsFormulaMode)); 
        }

        [ObservableProperty] public partial double? NrfgBaseValue { get; set; } = 60.0;   
        [ObservableProperty] public partial ObservableCollection<PolicyBoundaryItem> Boundaries { get; set; } = new();     

        // --- 4. DELETE MODAL STATE ---
        [ObservableProperty] public partial bool IsDeleteModalOpen { get; set; } = false;
        [ObservableProperty] public partial string DeleteModalTitle { get; set; } = string.Empty;
        [ObservableProperty] public partial string DeleteModalMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial bool CanConfirmDelete { get; set; } = false;
        private Centriku.Models.GradingTemplate? _templateToDelete = null;

        // --- 5. COMMANDS ---
        public IRelayCommand AddCategoryCommand { get; }
        public IRelayCommand<PolicyCategoryItem> RemoveCategoryCommand { get; }
        public IRelayCommand AddBoundaryCommand { get; }
        public IRelayCommand<PolicyBoundaryItem> RemoveBoundaryCommand { get; }
        
        public IRelayCommand SavePolicyCommand { get; }
        public IRelayCommand<Centriku.Models.GradingTemplate> EditTemplateCommand { get; }
        public IRelayCommand ResetFormCommand { get; }

        public IRelayCommand<Centriku.Models.GradingTemplate> InitiateDeleteCommand { get; }
        public IRelayCommand ConfirmDeleteCommand { get; }
        public IRelayCommand CancelDeleteCommand { get; }

        public PoliciesViewModel()
        {
            AddCategoryCommand = new RelayCommand(() => AddCategory("New Category", 0m));
            AddBoundaryCommand = new RelayCommand(AddBoundary);
            RemoveBoundaryCommand = new RelayCommand<PolicyBoundaryItem>(RemoveBoundary!);
            RemoveCategoryCommand = new RelayCommand<PolicyCategoryItem>(RemoveCategory!);
            SavePolicyCommand = new RelayCommand(SavePolicy, () => IsValidPolicy);
            
            EditTemplateCommand = new RelayCommand<Centriku.Models.GradingTemplate>(EditTemplate!);
            ResetFormCommand = new RelayCommand(ResetForm);

            InitiateDeleteCommand = new RelayCommand<Centriku.Models.GradingTemplate>(InitiateDelete!);
            ConfirmDeleteCommand = new RelayCommand(ConfirmDelete);
            CancelDeleteCommand = new RelayCommand(CancelDelete);

            ResetForm(); 
        }

        // --- CORE METHODS ---

        private async void EditTemplate(Centriku.Models.GradingTemplate template)
        {
            if (template == null) return;
            
            // Switch UI to Edit Mode
            IsEditing = true;
            _editingTemplateId = template.TemplateID;
            EditorTitleText = $"Editing: {template.TemplateName}";
            SaveButtonText = "Update Template";

            TemplateName = template.TemplateName ?? "Unnamed Template";
            PassingGrade = (decimal)template.PassingGrade;
            
            // Map DB string to UI String
            CalculationMode = template.CalculationMode == "CRG" ? "Boundary & Tier Scale (CRG)" : "Formula-Based Scale (NRFG)";
            
            var db = new Centriku.Services.DatabaseService().GetConnection();
            NrfgBaseValue = template.NrfgBaseValue;
            
            var savedCategories = await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == template.TemplateID).ToListAsync();
            var savedBoundaries = await db.Table<Centriku.Models.GradeBoundary>().Where(b => b.TemplateID == template.TemplateID).ToListAsync();
            
            Boundaries.Clear();
            foreach (var b in savedBoundaries) { Boundaries.Add(new PolicyBoundaryItem { MinScore = b.MinScore, MaxScore = b.MaxScore, Label = b.Label, GpaValue = b.GpaValue }); }

            foreach (var cat in Categories) { cat.PropertyChanged -= Category_PropertyChanged; }
            Categories.Clear();
            foreach (var dbCat in savedCategories) { AddCategory(dbCat.Name ?? "Category", (decimal)dbCat.Weight); }
        }

        private async void SavePolicy()
        {
            if (IsValidPolicy)
            {
                var db = new Centriku.Services.DatabaseService().GetConnection();
                
                // Map UI String back to DB String
                string dbCalcMode = IsBoundaryMode ? "CRG" : "NRFG";

                if (_editingTemplateId.HasValue)
                {
                    var templateToUpdate = await db.Table<Centriku.Models.GradingTemplate>().Where(t => t.TemplateID == _editingTemplateId.Value).FirstOrDefaultAsync();
                    templateToUpdate.TemplateName = this.TemplateName;
                    templateToUpdate.PassingGrade = (double)(this.PassingGrade ?? 0m);
                    templateToUpdate.CalculationMode = dbCalcMode;
                    templateToUpdate.NrfgBaseValue = this.NrfgBaseValue ?? 0.0;
                    
                    await db.UpdateAsync(templateToUpdate);

                    await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == templateToUpdate.TemplateID).DeleteAsync();
                    foreach (var uiCat in Categories) { await db.InsertAsync(new Centriku.Models.GradingCategory { TemplateID = templateToUpdate.TemplateID, Name = uiCat.Name, Weight = (double)(uiCat.Weight ?? 0m) }); }

                    await db.Table<Centriku.Models.GradeBoundary>().Where(b => b.TemplateID == templateToUpdate.TemplateID).DeleteAsync();
                    foreach (var b in Boundaries) { await db.InsertAsync(new Centriku.Models.GradeBoundary { TemplateID = templateToUpdate.TemplateID, MinScore = b.MinScore ?? 0, MaxScore = b.MaxScore ?? 0, Label = b.Label, GpaValue = b.GpaValue ?? 0 }); }
                }
                else
                {
                    var newTemplate = new Centriku.Models.GradingTemplate 
                    { 
                        TemplateName = this.TemplateName, 
                        PassingGrade = (double)(this.PassingGrade ?? 0m), 
                        CalculationMode = dbCalcMode,
                        NrfgBaseValue = this.NrfgBaseValue ?? 0.0 
                    };                    
                    await db.InsertAsync(newTemplate);

                    foreach (var uiCat in Categories) { await db.InsertAsync(new Centriku.Models.GradingCategory { TemplateID = newTemplate.TemplateID, Name = uiCat.Name, Weight = (double)(uiCat.Weight ?? 0m) }); }
                    foreach (var b in Boundaries) { await db.InsertAsync(new Centriku.Models.GradeBoundary { TemplateID = newTemplate.TemplateID, MinScore = b.MinScore ?? 0, MaxScore = b.MaxScore ?? 0, Label = b.Label, GpaValue = b.GpaValue ?? 0 }); }
                }

                await LoadSavedTemplatesAsync();
                ResetForm(); // Go back to create mode after a successful save!
            }
        }

        private void ResetForm()
        {
            // Switch UI back to Create Mode
            IsEditing = false;
            _editingTemplateId = null; 
            EditorTitleText = "✨ Create New Template";
            SaveButtonText = "Create Template";

            TemplateName = "New Grading Template";
            PassingGrade = 75m;
            CalculationMode = "Formula-Based Scale (NRFG)";
            NrfgBaseValue = 60.0;
            Boundaries.Clear();
            
            foreach (var cat in Categories) { cat.PropertyChanged -= Category_PropertyChanged; }
            Categories.Clear();
            
            // As requested, default weights start at 0%
            AddCategory("Written Work", 0m);
            AddCategory("Performance Tasks", 0m);
            AddCategory("Quarterly Assessment", 0m);
        }

        // --- SAFE DELETE MECHANISM ---

        private async void InitiateDelete(Centriku.Models.GradingTemplate template)
        {
            if (template == null) return;
            _templateToDelete = template;

            var db = new Centriku.Services.DatabaseService().GetConnection();
            
            // Check if any active TeacherClass is using this template
            var affectedClasses = await db.Table<Centriku.Models.TeacherClass>().Where(c => c.GradingTemplateID == template.TemplateID).ToListAsync();

            if (affectedClasses.Any())
            {
                CanConfirmDelete = false;
                DeleteModalTitle = "⚠️ Cannot Safely Delete Template";
                
                var classList = string.Join("\n", affectedClasses.Select(c => $"• {c.SubjectName} ({c.SectionLabel})"));
                DeleteModalMessage = $"This grading policy is currently assigned to {affectedClasses.Count} active class(es):\n\n{classList}\n\nDeleting this policy will disrupt their grade calculations. Please reassign these classes to a different policy first.";
            }
            else
            {
                CanConfirmDelete = true;
                DeleteModalTitle = "Delete Grading Template?";
                DeleteModalMessage = $"Are you sure you want to permanently delete the '{template.TemplateName}' template? This action cannot be undone.";
            }

            IsDeleteModalOpen = true;
        }

        private async void ConfirmDelete()
        {
            if (_templateToDelete != null && CanConfirmDelete)
            {
                var db = new Centriku.Services.DatabaseService().GetConnection();
                await db.DeleteAsync(_templateToDelete);
                await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == _templateToDelete.TemplateID).DeleteAsync();
                await db.Table<Centriku.Models.GradeBoundary>().Where(b => b.TemplateID == _templateToDelete.TemplateID).DeleteAsync();
                
                if (_editingTemplateId == _templateToDelete.TemplateID) ResetForm();
                await LoadSavedTemplatesAsync();
            }
            CancelDelete();
        }

        private void CancelDelete()
        {
            IsDeleteModalOpen = false;
            _templateToDelete = null;
        }

        // --- HELPER METHODS ---

        private void AddBoundary() => Boundaries.Add(new PolicyBoundaryItem { MinScore = 90, MaxScore = 100, Label = "A", GpaValue = 4.0 });
        private void RemoveBoundary(PolicyBoundaryItem boundary) { if (boundary != null && Boundaries.Contains(boundary)) Boundaries.Remove(boundary); }

        public async Task LoadSavedTemplatesAsync()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.CreateTableAsync<Centriku.Models.GradingTemplate>();
            await db.CreateTableAsync<Centriku.Models.GradingCategory>();
            await db.CreateTableAsync<Centriku.Models.GradeBoundary>();
            var templates = await db.Table<Centriku.Models.GradingTemplate>().ToListAsync();
            SavedTemplates.Clear();
            foreach (var t in templates) { SavedTemplates.Add(t); }
        }

        private void AddCategory(string name, decimal weight)
        {
            var category = new PolicyCategoryItem { Name = name, Weight = weight };
            category.PropertyChanged += Category_PropertyChanged; 
            Categories.Add(category);
            ValidateWeights();
        }

        private void RemoveCategory(PolicyCategoryItem category)
        {
            if (category != null && Categories.Contains(category))
            {
                category.PropertyChanged -= Category_PropertyChanged; 
                Categories.Remove(category);
                ValidateWeights();
            }
        }

        private void Category_PropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(PolicyCategoryItem.Weight)) ValidateWeights(); }

        private void ValidateWeights()
        {
            TotalWeight = Categories.Sum(c => c.Weight ?? 0m);
            IsValidPolicy = TotalWeight == 100m;
            ((RelayCommand)SavePolicyCommand).NotifyCanExecuteChanged();
        }
    }

    public partial class PolicyCategoryItem : ObservableObject
    {
        [ObservableProperty] public partial string Name { get; set; } = string.Empty;
        [ObservableProperty] public partial decimal? Weight { get; set; }
    }
    public partial class PolicyBoundaryItem : ObservableObject
    {
        [ObservableProperty] public partial double? MinScore { get; set; }
        [ObservableProperty] public partial double? MaxScore { get; set; }
        [ObservableProperty] public partial string? Label { get; set; }
        [ObservableProperty] public partial double? GpaValue { get; set; }
    }
}