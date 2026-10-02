using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels.Modals
{
    public partial class ManageQuestionsModalViewModel : ObservableObject
    {
        private readonly int _classId;
        private readonly Action _onQuestionsUpdated;
        private readonly Action _onClose;

        [ObservableProperty] public partial ObservableCollection<QuestionRowViewModel> QuestionBank { get; set; }
        [ObservableProperty] public partial string TotalQuestionsCountText { get; set; } = "Total Questions: 0";
        
        [ObservableProperty] public partial string NewQuestionInput { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewAnswerInput { get; set; } = string.Empty;

        public ManageQuestionsModalViewModel(int classId, ObservableCollection<QuestionRowViewModel> questionBank, Action onQuestionsUpdated, Action onClose)
        {
            _classId = classId;
            QuestionBank = questionBank;
            _onQuestionsUpdated = onQuestionsUpdated;
            _onClose = onClose;
            UpdateCount();
        }

        private void UpdateCount() => TotalQuestionsCountText = $"Total Questions: {QuestionBank.Count}";

        [RelayCommand]
        private async Task SaveNewQuestionAsync()
        {
            if (string.IsNullOrWhiteSpace(NewQuestionInput)) return;

            var db = new DatabaseService().GetConnection();
            var newQuestion = new RecitationQuestion
            {
                ClassID = _classId,
                QuestionText = NewQuestionInput.Trim(),
                AnswerText = NewAnswerInput?.Trim() ?? string.Empty,
                IsIncluded = true
            };
            
            await db.InsertAsync(newQuestion);
            QuestionBank.Add(new QuestionRowViewModel(newQuestion));
            
            NewQuestionInput = string.Empty;
            NewAnswerInput = string.Empty;
            
            UpdateCount();
            _onQuestionsUpdated(); 
        }

        [RelayCommand]
        private async Task DeleteQuestionAsync(QuestionRowViewModel qRow)
        {
            if (qRow == null) return;
            var db = new DatabaseService().GetConnection();
            await db.DeleteAsync(qRow.DbModel);
            
            QuestionBank.Remove(qRow);
            UpdateCount();
            _onQuestionsUpdated();
        }

        [RelayCommand]
        private async Task EditOrSaveQuestionAsync(QuestionRowViewModel qRow)
        {
            if (qRow == null) return;
            
            if (!qRow.IsEditing)
            {
                qRow.IsEditing = true; 
            }
            else
            {
                var db = new DatabaseService().GetConnection();
                await db.UpdateAsync(qRow.DbModel);
                qRow.IsEditing = false; 
                _onQuestionsUpdated(); 
            }
        }

        [RelayCommand]
        private async Task ToggleQuestionIncludedAsync(QuestionRowViewModel qRow)
        {
            if (qRow == null) return;
            var db = new DatabaseService().GetConnection();
            await db.UpdateAsync(qRow.DbModel);
            _onQuestionsUpdated(); 
        }

        [RelayCommand]
        private void Close() => _onClose();
    }
}