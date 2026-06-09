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
        [ObservableProperty] public partial ObservableCollection<PolicyCategoryItem> Categories { get; set; }
        [ObservableProperty] public partial ObservableCollection<Centriku.Models.GradingTemplate> SavedTemplates { get; set; } = new();
        [ObservableProperty] public partial string TemplateName { get; set; } = "Standard High School";

        [ObservableProperty] public partial string CalculationMode { get; set; } = "NRFG";
        public ObservableCollection<string> AvailableCalculationModes { get; } = ["CRG", "NRFG"];
        
        public bool IsBoundaryMode => CalculationMode == "CRG";
        public bool IsFormulaMode => CalculationMode == "NRFG"; 
        
        partial void OnCalculationModeChanged(string value) 
        { 
            OnPropertyChanged(nameof(IsBoundaryMode)); 
            OnPropertyChanged(nameof(IsFormulaMode)); 
        }

        [ObservableProperty] public partial double? NrfgBaseValue { get; set; } = 60.0;   
        [ObservableProperty] public partial ObservableCollection<PolicyBoundaryItem> Boundaries { get; set; } = new();     
        public IRelayCommand AddBoundaryCommand { get; }
        public IRelayCommand<PolicyBoundaryItem> RemoveBoundaryCommand { get; }

        [ObservableProperty] public partial decimal? PassingGrade { get; set; } = 75m;

        [ObservableProperty] public partial decimal TotalWeight { get; set; }

        [ObservableProperty] public partial bool IsValidPolicy { get; set; }
        private int? _editingTemplateId = null;

        public IRelayCommand AddCategoryCommand { get; }
        public IRelayCommand<PolicyCategoryItem> RemoveCategoryCommand { get; }
        public IRelayCommand SavePolicyCommand { get; }
        public IRelayCommand<Centriku.Models.GradingTemplate> EditTemplateCommand { get; }
        public IRelayCommand<Centriku.Models.GradingTemplate> DeleteTemplateCommand { get; }
        public IRelayCommand ResetFormCommand { get; }

        public PoliciesViewModel()
        {
            Categories = [];
            
            AddCategoryCommand = new RelayCommand(() => AddCategory("New Category", 0m));
            AddBoundaryCommand = new RelayCommand(AddBoundary);
            RemoveBoundaryCommand = new RelayCommand<PolicyBoundaryItem>(RemoveBoundary!);
            RemoveCategoryCommand = new RelayCommand<PolicyCategoryItem>(RemoveCategory!);
            SavePolicyCommand = new RelayCommand(SavePolicy, () => IsValidPolicy);
            
            EditTemplateCommand = new RelayCommand<Centriku.Models.GradingTemplate>(EditTemplate!);
            DeleteTemplateCommand = new RelayCommand<Centriku.Models.GradingTemplate>(DeleteTemplate!);
            ResetFormCommand = new RelayCommand(ResetForm);

            ResetForm(); 
        }

        private async void EditTemplate(Centriku.Models.GradingTemplate template)
        {
            if (template == null) return;
            _editingTemplateId = template.TemplateID;
            TemplateName = template.TemplateName ?? "Unnamed Template";
            PassingGrade = (decimal)template.PassingGrade;
            CalculationMode = template.CalculationMode ?? "NRFG";
            var db = new Centriku.Services.DatabaseService().GetConnection();
            NrfgBaseValue = template.NrfgBaseValue;
            var savedCategories = await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == template.TemplateID).ToListAsync();

            var savedBoundaries = await db.Table<Centriku.Models.GradeBoundary>().Where(b => b.TemplateID == template.TemplateID).ToListAsync();
            Boundaries.Clear();
            foreach (var b in savedBoundaries) 
            {
                Boundaries.Add(new PolicyBoundaryItem { MinScore = b.MinScore, MaxScore = b.MaxScore, Label = b.Label, GpaValue = b.GpaValue });
            }

            foreach (var cat in Categories) { cat.PropertyChanged -= Category_PropertyChanged; }
            Categories.Clear();

            foreach (var dbCat in savedCategories)
            {
                AddCategory(dbCat.Name ?? "Category", (decimal)dbCat.Weight);
            }
        }

        private async void DeleteTemplate(Centriku.Models.GradingTemplate template)
        {
            if (template == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.DeleteAsync(template);
            await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == template.TemplateID).DeleteAsync();
            
            await db.Table<Centriku.Models.GradeBoundary>().Where(b => b.TemplateID == template.TemplateID).DeleteAsync();
            if (_editingTemplateId == template.TemplateID) ResetForm();

            await LoadSavedTemplatesAsync();
        }

        private async void SavePolicy()
        {
            if (IsValidPolicy)
            {
                var db = new Centriku.Services.DatabaseService().GetConnection();

                if (_editingTemplateId.HasValue)
                {
                    // === UPDATE MODE ===
                    var templateToUpdate = await db.Table<Centriku.Models.GradingTemplate>().Where(t => t.TemplateID == _editingTemplateId.Value).FirstOrDefaultAsync();
                    templateToUpdate.TemplateName = this.TemplateName;
                    
                    // Safely coalesce nulls to 0 before hitting the DB!
                    templateToUpdate.PassingGrade = (double)(this.PassingGrade ?? 0m);
                    templateToUpdate.CalculationMode = this.CalculationMode;
                    templateToUpdate.NrfgBaseValue = this.NrfgBaseValue ?? 0.0;
                    await db.UpdateAsync(templateToUpdate);

                    await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == templateToUpdate.TemplateID).DeleteAsync();
                    foreach (var uiCat in Categories)
                    {
                        await db.InsertAsync(new Centriku.Models.GradingCategory { TemplateID = templateToUpdate.TemplateID, Name = uiCat.Name, Weight = (double)(uiCat.Weight ?? 0m) });
                    }

                    await db.Table<Centriku.Models.GradeBoundary>().Where(b => b.TemplateID == templateToUpdate.TemplateID).DeleteAsync();
                    foreach (var b in Boundaries) 
                    { 
                        // Map the UI wrapper back to the raw SQLite Database model
                        await db.InsertAsync(new Centriku.Models.GradeBoundary 
                        {
                            TemplateID = templateToUpdate.TemplateID,
                            MinScore = b.MinScore ?? 0,
                            MaxScore = b.MaxScore ?? 0,
                            Label = b.Label,
                            GpaValue = b.GpaValue ?? 0
                        }); 
                    }

                    System.Console.WriteLine($"SUCCESS: Updated Template #{templateToUpdate.TemplateID}!");
                }
                else
                {
                    // === CREATE MODE ===
                    var newTemplate = new Centriku.Models.GradingTemplate 
                    { 
                        TemplateName = this.TemplateName, 
                        PassingGrade = (double)(this.PassingGrade ?? 0m), 
                        CalculationMode = this.CalculationMode,
                        NrfgBaseValue = this.NrfgBaseValue ?? 0.0 
                    };                    
                    await db.InsertAsync(newTemplate);

                    foreach (var uiCat in Categories)
                    {
                        await db.InsertAsync(new Centriku.Models.GradingCategory { TemplateID = newTemplate.TemplateID, Name = uiCat.Name, Weight = (double)(uiCat.Weight ?? 0m) });
                    }

                    foreach (var b in Boundaries) 
                    { 
                        await db.InsertAsync(new Centriku.Models.GradeBoundary 
                        {
                            TemplateID = newTemplate.TemplateID,
                            MinScore = b.MinScore ?? 0,
                            MaxScore = b.MaxScore ?? 0,
                            Label = b.Label,
                            GpaValue = b.GpaValue ?? 0
                        });  
                    }
                    System.Console.WriteLine($"SUCCESS: Created New Template #{newTemplate.TemplateID}!");
                }

                await LoadSavedTemplatesAsync();
            }
        }

        private void ResetForm()
        {
            _editingTemplateId = null; 
            TemplateName = "New Grading Template";
            PassingGrade = 75m;
            CalculationMode = "NRFG";
            NrfgBaseValue = 60.0;
            Boundaries.Clear();
            
            foreach (var cat in Categories) { cat.PropertyChanged -= Category_PropertyChanged; }
            Categories.Clear();
            AddCategory("Written Work", 30m);
            AddCategory("Performance Tasks", 50m);
            AddCategory("Quarterly Assessment", 20m);
        }

        private void AddBoundary()
        {
            Boundaries.Add(new PolicyBoundaryItem { MinScore = 90, MaxScore = 100, Label = "A", GpaValue = 4.0 });
        }

        private void RemoveBoundary(PolicyBoundaryItem boundary)
        {
            if (boundary != null && Boundaries.Contains(boundary)) Boundaries.Remove(boundary);
        }

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

        // Validation & Helper methods remain exactly the same
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

        private void Category_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PolicyCategoryItem.Weight)) ValidateWeights();
        }

        private void ValidateWeights()
        {
            TotalWeight = Categories.Sum(c => c.Weight ?? 0m);
            IsValidPolicy = TotalWeight == 100m;
            ((RelayCommand)SavePolicyCommand).NotifyCanExecuteChanged();
        }
    }

    public partial class PolicyCategoryItem : ObservableObject
    {
        [ObservableProperty]
        public partial string Name { get; set; } = string.Empty;

        [ObservableProperty]
        public partial decimal? Weight { get; set; }
    }
    public partial class PolicyBoundaryItem : ObservableObject
    {
        [ObservableProperty] public partial double? MinScore { get; set; }
        [ObservableProperty] public partial double? MaxScore { get; set; }
        [ObservableProperty] public partial string? Label { get; set; }
        [ObservableProperty] public partial double? GpaValue { get; set; }
    }
}