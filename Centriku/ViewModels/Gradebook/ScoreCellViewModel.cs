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
                    OnPropertyChanged(nameof(PointsEarnedDisplay));
                    SaveScoreToDatabase(); 
                    _onScoreChanged?.Invoke(); 
                }
            }
        }
        public string PointsEarnedDisplay
        {
            get => PointsEarned.ToString("0.##"); // Strips trailing decimals for a clean UI
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    PointsEarned = 0; // If they delete everything, safely treat it as a 0
                }
                else if (double.TryParse(value, out double numericValue))
                {
                    PointsEarned = numericValue; // If it's a valid number, save it!
                }
                else
                {
                    // If they typed letters (e.g., "abc"), reject it and revert the UI to the last valid number!
                    OnPropertyChanged(); 
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