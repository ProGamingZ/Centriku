using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels
{
    public partial class PoliciesViewModel : ViewModelBase
    {
        // Holds the list of categories currently visible on the screen
        [ObservableProperty]
        public partial ObservableCollection<GradingCategory> Categories { get; set; }
        public IRelayCommand AddCategoryCommand { get; }
        public IRelayCommand<GradingCategory> RemoveCategoryCommand { get; }

        public PoliciesViewModel()
        {
            // Initializing with a standard default grading template
            Categories =
            [
                new GradingCategory { Name = "Written Work", Weight = 30m },
                new GradingCategory { Name = "Performance Tasks", Weight = 50m },
                new GradingCategory { Name = "Quarterly Assessment", Weight = 20m }
            ];

            AddCategoryCommand = new RelayCommand(AddCategory);
            RemoveCategoryCommand = new RelayCommand<GradingCategory>(RemoveCategory!);
        }

        private void AddCategory()
        {
            Categories.Add(new GradingCategory { Name = "New Category", Weight = 0m });
        }

        private void RemoveCategory(GradingCategory category)
        {
            if (category != null && Categories.Contains(category))
            {
                Categories.Remove(category);
            }
        }
    }

    // Represents a single row in the policy builder
    public partial class GradingCategory : ObservableObject
    {
        [ObservableProperty]
        public partial string Name { get; set; } = string.Empty;

        [ObservableProperty]
        public partial decimal? Weight { get; set; }
    }
}