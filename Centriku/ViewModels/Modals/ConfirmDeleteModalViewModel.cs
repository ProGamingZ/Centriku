using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Centriku.ViewModels.Modals
{
    public partial class ConfirmDeleteModalViewModel : ObservableObject
    {
        private readonly Func<Task> _onConfirmAsync;
        private readonly Action _onClose;

        [ObservableProperty] public partial string Title { get; set; }
        [ObservableProperty] public partial string Message { get; set; }
        [ObservableProperty] public partial bool IsProcessing { get; set; } = false;

        public ConfirmDeleteModalViewModel(string title, string message, Func<Task> onConfirmAsync, Action onClose)
        {
            Title = title;
            Message = message;
            _onConfirmAsync = onConfirmAsync;
            _onClose = onClose;
        }

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            IsProcessing = true;
            try
            {
                await _onConfirmAsync();
            }
            finally
            {
                IsProcessing = false;
                _onClose();
            }
        }

        [RelayCommand]
        private void Cancel() => _onClose();
    }
}