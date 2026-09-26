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
        public System.Action<string>? ShowToastMessage { get; set; }
        [ObservableProperty] public partial bool IsProcessing { get; set; } = false;
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
        [ObservableProperty] public partial double? NrfgBaseValue { get; set; } = 50.0;
        [ObservableProperty] public partial decimal TotalWeight { get; set; }
        [ObservableProperty] public partial bool IsValidPolicy { get; set; }

        // --- 3. DELETE MODAL STATE ---
        [ObservableProperty] public partial bool IsDeleteModalOpen { get; set; } = false;
        [ObservableProperty] public partial string DeleteModalTitle { get; set; } = string.Empty;
        [ObservableProperty] public partial string DeleteModalMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial bool CanConfirmDelete { get; set; } = false;
        private Centriku.Models.GradingTemplate? _templateToDelete = null;

        // --- 4. COMMANDS ---
        public IRelayCommand AddCategoryCommand { get; }
        public IRelayCommand<PolicyCategoryItem> RemoveCategoryCommand { get; }
        public IAsyncRelayCommand SavePolicyCommand { get; }
        public IRelayCommand<Centriku.Models.GradingTemplate> EditTemplateCommand { get; }
        public IRelayCommand ResetFormCommand { get; }
        public IRelayCommand<Centriku.Models.GradingTemplate> InitiateDeleteCommand { get; }
        public IAsyncRelayCommand ConfirmDeleteCommand { get; }
        public IRelayCommand CancelDeleteCommand { get; }

        public PoliciesViewModel()
        {
            AddCategoryCommand = new RelayCommand(() => AddCategory("New Category", 0m));
            RemoveCategoryCommand = new RelayCommand<PolicyCategoryItem>(RemoveCategory!);
            SavePolicyCommand = new AsyncRelayCommand(SavePolicyAsync, () => IsValidPolicy);
            
            EditTemplateCommand = new RelayCommand<Centriku.Models.GradingTemplate>(EditTemplate!);
            ResetFormCommand = new RelayCommand(ResetForm);

            InitiateDeleteCommand = new RelayCommand<Centriku.Models.GradingTemplate>(InitiateDelete!);
            ConfirmDeleteCommand = new AsyncRelayCommand(ConfirmDeleteAsync);
            CancelDeleteCommand = new RelayCommand(CancelDelete);

            ResetForm(); 
        }

        private async void EditTemplate(Centriku.Models.GradingTemplate template)
        {
            if (template == null) return;
            IsEditing = true;
            _editingTemplateId = template.TemplateID;
            EditorTitleText = $"Editing: {template.TemplateName}";
            SaveButtonText = "Update Template";

            TemplateName = template.TemplateName ?? "Unnamed Template";
            PassingGrade = (decimal)template.PassingGrade;
            NrfgBaseValue = template.NrfgBaseValue;
            
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var savedCategories = await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == template.TemplateID).ToListAsync();
            
            foreach (var cat in Categories) { cat.PropertyChanged -= Category_PropertyChanged; }
            Categories.Clear();
            foreach (var dbCat in savedCategories) { AddCategory(dbCat.Name ?? "Category", (decimal)dbCat.Weight); }
        }

        private async Task SavePolicyAsync()
        {
            if (!IsValidPolicy) return;

            IsProcessing = true;

            try
            {
                var db = new Centriku.Services.DatabaseService().GetConnection();
                
                // DEFENSIVE: Sanitize the template name
                string safeTemplateName = string.IsNullOrWhiteSpace(TemplateName) ? "Unnamed Policy" : TemplateName.Trim();

                if (_editingTemplateId.HasValue)
                {
                    // TRANSACTION: Protects against corruption if the app crashes halfway!
                    await db.RunInTransactionAsync(tran => 
                    {
                        var templateToUpdate = tran.Table<Centriku.Models.GradingTemplate>().FirstOrDefault(t => t.TemplateID == _editingTemplateId.Value);
                        if (templateToUpdate != null)
                        {
                            templateToUpdate.TemplateName = safeTemplateName;
                            templateToUpdate.PassingGrade = (double)(this.PassingGrade ?? 0m);
                            templateToUpdate.NrfgBaseValue = this.NrfgBaseValue ?? 0.0;
                            tran.Update(templateToUpdate);

                            // Wipe old categories
                            tran.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == templateToUpdate.TemplateID).Delete();
                            
                            // ALIGNMENT: Must strictly start at 0 for UI Arrays!
                            int order = 0;
                            foreach (var uiCat in Categories) 
                            { 
                                tran.Insert(new Centriku.Models.GradingCategory { 
                                    TemplateID = templateToUpdate.TemplateID, 
                                    Name = uiCat.Name?.Trim() ?? "Unknown", // DEFENSIVE: Strip invisible characters!
                                    Weight = (double)(uiCat.Weight ?? 0m),
                                    SequenceOrder = order++ 
                                }); 
                            }
                        }
                    });
                    
                    ShowToastMessage?.Invoke($"Policy '{safeTemplateName}' updated successfully.");
                }
                else
                {
                    var newTemplate = new Centriku.Models.GradingTemplate 
                    { 
                        TemplateName = safeTemplateName, 
                        PassingGrade = (double)(this.PassingGrade ?? 0m), 
                        NrfgBaseValue = this.NrfgBaseValue ?? 0.0 
                    };                    
                    await db.InsertAsync(newTemplate);

                    // ALIGNMENT: Must strictly start at 0!
                    int orderCreate = 0;
                    var newCategories = Categories.Select(uiCat => new Centriku.Models.GradingCategory 
                    {
                        TemplateID = newTemplate.TemplateID, 
                        Name = uiCat.Name?.Trim() ?? "Unknown", // DEFENSIVE: Strip invisible characters!
                        Weight = (double)(uiCat.Weight ?? 0m),
                        SequenceOrder = orderCreate++
                    }).ToList();

                    await db.InsertAllAsync(newCategories);
                    ShowToastMessage?.Invoke($"New policy '{safeTemplateName}' created successfully.");
                }

                await LoadSavedTemplatesAsync();
                ResetForm(); 
            }
            catch (System.Exception ex)
            {
                ShowToastMessage?.Invoke($"Error saving policy: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void ResetForm()
        {
            IsEditing = false;
            _editingTemplateId = null; 
            EditorTitleText = "Create New Template";
            SaveButtonText = "Create Template";

            TemplateName = "New Grading Template";
            PassingGrade = 75m;
            NrfgBaseValue = 50.0;
            
            foreach (var cat in Categories) { cat.PropertyChanged -= Category_PropertyChanged; }
            Categories.Clear();
            
            AddCategory("Class Standing", 0m);
            AddCategory("Major Course Output", 0m);
            AddCategory("Major Exam", 0m);
        }

        private async void InitiateDelete(Centriku.Models.GradingTemplate template)
        {
            if (template == null) return;
            _templateToDelete = template;

            var db = new Centriku.Services.DatabaseService().GetConnection();
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

        private async Task ConfirmDeleteAsync()
        {
            if (_templateToDelete != null && CanConfirmDelete)
            {
                IsProcessing = true;
                try
                {
                    var db = new Centriku.Services.DatabaseService().GetConnection();
                    
                    // Transaction ensures both the template and its categories die together
                    await db.RunInTransactionAsync(tran => 
                    {
                        tran.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == _templateToDelete.TemplateID).Delete();
                        tran.Delete(_templateToDelete);
                    });
                    
                    if (_editingTemplateId == _templateToDelete.TemplateID) ResetForm();
                    await LoadSavedTemplatesAsync();
                    ShowToastMessage?.Invoke($"Deleted policy: '{_templateToDelete.TemplateName}'");
                }
                catch(System.Exception ex)
                {
                    ShowToastMessage?.Invoke($"Error deleting policy: {ex.Message}");
                }
                finally
                {
                    IsProcessing = false;
                }
            }
            CancelDelete();
        }

        private void CancelDelete()
        {
            IsDeleteModalOpen = false;
            _templateToDelete = null;
        }

        public async Task LoadSavedTemplatesAsync()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.CreateTableAsync<Centriku.Models.GradingTemplate>();
            await db.CreateTableAsync<Centriku.Models.GradingCategory>();
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
            ((AsyncRelayCommand)SavePolicyCommand).NotifyCanExecuteChanged();

        }
    }

    public partial class PolicyCategoryItem : ObservableObject
    {
        [ObservableProperty] public partial string Name { get; set; } = string.Empty;
        [ObservableProperty] public partial decimal? Weight { get; set; }
    }
}