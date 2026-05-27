using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;

namespace Centriku.ViewModels
{
    public partial class ScoreCellViewModel : ObservableObject
    {
        public Score DbModel { get; }
        public double MaxScore { get; }
        private readonly System.Action _onScoreChanged; 

        public double PointsEarned
        {
            get => DbModel.PointsEarned;
            set
            {
                double finalValue = value;
                if (finalValue > MaxScore) finalValue = MaxScore; 
                if (finalValue < 0) finalValue = 0;               

                if (DbModel.PointsEarned != finalValue)
                {
                    DbModel.PointsEarned = finalValue;
                    OnPropertyChanged(); 
                    SaveScoreToDatabase(); 
                    _onScoreChanged?.Invoke(); 
                }
            }
        }

        public ScoreCellViewModel(Score score, double maxScore, System.Action onScoreChanged)
        {
            DbModel = score;
            MaxScore = maxScore;
            _onScoreChanged = onScoreChanged;
        }

        private async void SaveScoreToDatabase()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            if (DbModel.ScoreID == 0) await db.InsertAsync(DbModel);
            else await db.UpdateAsync(DbModel);
        }
    }
}