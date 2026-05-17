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

        [ObservableProperty] public partial bool UseTransmutation { get; set; } = true;

        [ObservableProperty] public partial decimal PassingGrade { get; set; } = 75m;

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
            Categories = new ObservableCollection<PolicyCategoryItem>();
            
            AddCategoryCommand = new RelayCommand(() => AddCategory("New Category", 0m));
            RemoveCategoryCommand = new RelayCommand<PolicyCategoryItem>(RemoveCategory!);
            SavePolicyCommand = new RelayCommand(SavePolicy, () => IsValidPolicy);
            
            EditTemplateCommand = new RelayCommand<Centriku.Models.GradingTemplate>(EditTemplate!);
            DeleteTemplateCommand = new RelayCommand<Centriku.Models.GradingTemplate>(DeleteTemplate!);
            ResetFormCommand = new RelayCommand(ResetForm);

            ResetForm(); 
            LoadSavedTemplates();
        }

        private async void EditTemplate(Centriku.Models.GradingTemplate template)
        {
            if (template == null) return;
            _editingTemplateId = template.TemplateID;
            TemplateName = template.TemplateName ?? "Unnamed Template";
            PassingGrade = (decimal)template.PassingGrade;
            UseTransmutation = template.UseTransmutation;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            var savedCategories = await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == template.TemplateID).ToListAsync();

            foreach (var cat in Categories) { cat.PropertyChanged -= Category_PropertyChanged; }
            Categories.Clear();

            foreach (var dbCat in savedCategories)
            {
                AddCategory(dbCat.Name ?? "Category", (decimal)dbCat.Weight);
            }
        }

        // --- NEW: The Delete Logic ---
        private async void DeleteTemplate(Centriku.Models.GradingTemplate template)
        {
            if (template == null) return;
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.DeleteAsync(template);
            await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == template.TemplateID).DeleteAsync();
            if (_editingTemplateId == template.TemplateID) ResetForm();

            LoadSavedTemplates();
        }

        private async void SavePolicy()
        {
            if (IsValidPolicy)
            {
                var db = new Centriku.Services.DatabaseService().GetConnection();

                if (_editingTemplateId.HasValue)
                {
                    var templateToUpdate = await db.Table<Centriku.Models.GradingTemplate>().Where(t => t.TemplateID == _editingTemplateId.Value).FirstOrDefaultAsync();
                    templateToUpdate.TemplateName = this.TemplateName;
                    templateToUpdate.PassingGrade = (double)this.PassingGrade;
                    templateToUpdate.UseTransmutation = this.UseTransmutation;
                    await db.UpdateAsync(templateToUpdate);

                    // To update Table 8 easily, we delete the old categories and insert the new ones
                    await db.Table<Centriku.Models.GradingCategory>().Where(c => c.TemplateID == templateToUpdate.TemplateID).DeleteAsync();
                    foreach (var uiCat in Categories)
                    {
                        await db.InsertAsync(new Centriku.Models.GradingCategory { TemplateID = templateToUpdate.TemplateID, Name = uiCat.Name, Weight = (double)(uiCat.Weight ?? 0m) });
                    }
                    System.Console.WriteLine($"SUCCESS: Updated Template #{templateToUpdate.TemplateID}!");
                }
                else
                {
                    var newTemplate = new Centriku.Models.GradingTemplate { TemplateName = this.TemplateName, PassingGrade = (double)this.PassingGrade, UseTransmutation = this.UseTransmutation };
                    await db.InsertAsync(newTemplate);
                    foreach (var uiCat in Categories)
                    {
                        await db.InsertAsync(new Centriku.Models.GradingCategory { TemplateID = newTemplate.TemplateID, Name = uiCat.Name, Weight = (double)(uiCat.Weight ?? 0m) });
                    }
                    System.Console.WriteLine($"SUCCESS: Created New Template #{newTemplate.TemplateID}!");
                }

                LoadSavedTemplates();
            }
        }

        private void ResetForm()
        {
            _editingTemplateId = null; 
            TemplateName = "New Grading Template";
            PassingGrade = 75m;
            UseTransmutation = true;
            
            foreach (var cat in Categories) { cat.PropertyChanged -= Category_PropertyChanged; }
            Categories.Clear();
            AddCategory("Written Work", 30m);
            AddCategory("Performance Tasks", 50m);
            AddCategory("Quarterly Assessment", 20m);
        }

        private async void LoadSavedTemplates()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
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
}