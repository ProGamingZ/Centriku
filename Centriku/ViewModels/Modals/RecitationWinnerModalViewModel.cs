using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels.Modals
{
    public partial class RecitationWinnerModalViewModel : ObservableObject
    {
        private readonly Action _onClose;

        [ObservableProperty] public partial string StudentName { get; set; } = string.Empty;
        [ObservableProperty] public partial string QuestionText { get; set; } = string.Empty;
        [ObservableProperty] public partial string AnswerText { get; set; } = string.Empty;
        [ObservableProperty] public partial bool HasAnswer { get; set; } = false;
        [ObservableProperty] public partial bool IsAnswerRevealed { get; set; } = false;

        public RecitationWinnerModalViewModel(string studentName, string question, string answer, Action onClose)
        {
            StudentName = studentName;
            QuestionText = question;
            AnswerText = answer;
            HasAnswer = !string.IsNullOrWhiteSpace(answer);
            _onClose = onClose;
        }

        [RelayCommand]
        private void ToggleAnswerVisibility() => IsAnswerRevealed = !IsAnswerRevealed;

        [RelayCommand]
        private void Close() => _onClose();
    }
}