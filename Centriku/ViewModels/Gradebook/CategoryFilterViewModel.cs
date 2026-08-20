using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Centriku.ViewModels
{
    public partial class CategoryFilterViewModel : ObservableObject
    {
        public string CategoryName { get; }
        public int SequenceOrder { get; set; }
        public ObservableCollection<AssessmentFilterViewModel> Assessments { get; }

        public bool IsCategoryVisible
        {
            get => Assessments.Any(a => a.IsVisible); 
            set
            {
                foreach (var a in Assessments)
                {
                    a.IsVisible = value; 
                }
                OnPropertyChanged();
            }
        }

        public CategoryFilterViewModel(string name, System.Collections.Generic.IEnumerable<AssessmentFilterViewModel> assessments)
        {
            CategoryName = name;
            SequenceOrder = 0; // Default value, will be safely assigned by the UI generator
            Assessments = new ObservableCollection<AssessmentFilterViewModel>(assessments);
            
            foreach(var a in Assessments) 
            {
                a.PropertyChanged += (s,e) => 
                {
                    if (e.PropertyName == nameof(AssessmentFilterViewModel.IsVisible))
                        OnPropertyChanged(nameof(IsCategoryVisible));
                };
            }
        }
    }
}