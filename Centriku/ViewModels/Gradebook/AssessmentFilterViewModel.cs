using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels
{
    public partial class AssessmentFilterViewModel(IDatabaseService dbService, Assessment assessment, System.Action onVisibilityChanged) : ObservableObject
    {
        private readonly IDatabaseService _dbService = dbService;
        public Assessment DbModel { get; } = assessment;
        private readonly System.Action _onVisibilityChanged = onVisibilityChanged;

        public string Title => DbModel.Title ?? "Unknown";

        public bool IsVisible
        {
            get => DbModel.IsVisible;
            set
            {
                if (DbModel.IsVisible != value)
                {
                    DbModel.IsVisible = value;
                    OnPropertyChanged();
                    SaveToDb();
                    _onVisibilityChanged?.Invoke(); 
                }
            }
        }
        // Updates visibility without firing a full DataGrid column rebuild on every loop item
        public void SetVisibilitySilently(bool value)
        {
            if (DbModel.IsVisible != value)
            {
                DbModel.IsVisible = value;
                OnPropertyChanged(nameof(IsVisible));
                SaveToDb();
            }
        }       
        public void TriggerRedraw() => _onVisibilityChanged?.Invoke();
        private async void SaveToDb()
        {
            var db = _dbService.GetConnection();
            await db.UpdateAsync(DbModel);
        }
    }
}