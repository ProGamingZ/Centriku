using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;

namespace Centriku.ViewModels
{
    public partial class AssessmentFilterViewModel : ObservableObject
    {
        public Assessment DbModel { get; }
        private readonly System.Action _onVisibilityChanged;

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

        public AssessmentFilterViewModel(Assessment assessment, System.Action onVisibilityChanged)
        {
            DbModel = assessment;
            _onVisibilityChanged = onVisibilityChanged;
        }

        private async void SaveToDb()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            await db.UpdateAsync(DbModel);
        }
    }
}