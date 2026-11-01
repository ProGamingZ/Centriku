using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels
{
    public partial class ScoreCellViewModel(IDatabaseService dbService, Score score, double maxScore, System.Action onScoreChanged) : ObservableObject
    {
        private readonly IDatabaseService _dbService = dbService;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        public Score DbModel { get; } = score;
        public double MaxScore { get; } = maxScore;
        private readonly System.Action _onScoreChanged = onScoreChanged; 

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
                    _ = SaveScoreToDatabaseAsync(); 
                    _onScoreChanged?.Invoke(); 
                }
            }
        }

        public string PointsEarnedDisplay
        {
            get => PointsEarned.ToString("0.##");
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    PointsEarned = 0;
                }
                else if (double.TryParse(value, out double numericValue))
                {
                    PointsEarned = numericValue;
                }
                else
                {
                    OnPropertyChanged(); 
                }
            }
        }

        private async Task SaveScoreToDatabaseAsync()
        {
            await _saveLock.WaitAsync();
            try
            {
                var db = _dbService.GetConnection();
                if (DbModel.ScoreID == 0) await db.InsertAsync(DbModel);
                else await db.UpdateAsync(DbModel);
            }
            finally
            {
                _saveLock.Release();
            }
        }
    }
}