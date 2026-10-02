using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;

namespace Centriku.ViewModels
{
    public partial class QuestionRowViewModel : ObservableObject
    {
        public RecitationQuestion DbModel { get; }
        
        [ObservableProperty] public partial bool IsEditing { get; set; } = false;
        
        public string QuestionText { get => DbModel.QuestionText; set { DbModel.QuestionText = value; OnPropertyChanged(); } }
        public string AnswerText { get => DbModel.AnswerText; set { DbModel.AnswerText = value; OnPropertyChanged(); } }
        public bool IsIncluded { get => DbModel.IsIncluded; set { DbModel.IsIncluded = value; OnPropertyChanged(); } }

        public QuestionRowViewModel(RecitationQuestion model) { DbModel = model; }
    }
}