using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels
{
    public partial class GradebookViewModel
    {
        // Lists for the UI boxes
        [ObservableProperty] public partial ObservableCollection<StudentGradeRow> RemainingStudents { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<StudentGradeRow> ChosenStudents { get; set; } = new();

        // UI Text
        [ObservableProperty] public partial string RecitationWinnerName { get; set; } = "Ready to Spin!";
        [ObservableProperty] public partial bool IsSpinning { get; set; } = false;

        // An event we will fire to tell the XAML Code-Behind to trigger the physical wheel animation
        public event Action<int>? OnSpinWheelRequested;
        public event Action? OnWheelResetRequested;

        private async Task LoadRecitationData()
        {
            var db = new DatabaseService().GetConnection();
            
            // Re-apply the new column if it doesn't exist yet
            await db.CreateTableAsync<ClassRoster>(); 

            var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
            
            var remaining = new System.Collections.Generic.List<StudentGradeRow>();
            var chosen = new System.Collections.Generic.List<StudentGradeRow>();

            // GradebookRows is already loaded with the active students from LoadGradebookData!
            foreach (var studentRow in GradebookRows)
            {
                var rosterEntry = roster.FirstOrDefault(r => r.StudentID == studentRow.StudentID);
                if (rosterEntry != null && rosterEntry.HasRecited)
                {
                    chosen.Add(studentRow);
                }
                else
                {
                    remaining.Add(studentRow);
                }
            }

            RemainingStudents.Clear();
            foreach (var s in remaining) RemainingStudents.Add(s);

            ChosenStudents.Clear();
            foreach (var s in chosen) ChosenStudents.Add(s);
            RecitationWinnerName = "Ready to Spin!";
        }

        [RelayCommand]
        public async Task SkipRecitationAsync()
        {
            if (IsSpinning || !RemainingStudents.Any()) return;

            // Instantly pick a random winner
            var random = new Random();
            int winnerIndex = random.Next(RemainingStudents.Count);
            var winner = RemainingStudents[winnerIndex];

            // Instantly process the winner without animation
            await ProcessWinnerAsync(winner);
        }

        [RelayCommand]
        public void SpinRecitation()
        {
            if (IsSpinning || !RemainingStudents.Any()) return;

            IsSpinning = true;
            RecitationWinnerName = "Spinning...";

            // 1. Pick the winner
            var random = new Random();
            int winnerIndex = random.Next(RemainingStudents.Count);
            
            // 2. Fire the event to tell the UI to physically spin the wheel to this specific index
            OnSpinWheelRequested?.Invoke(winnerIndex);
        }

        // The UI will call this method when the 3-second spin animation finishes
        public async Task WheelAnimationCompletedAsync(int winnerIndex)
        {
            if (winnerIndex >= 0 && winnerIndex < RemainingStudents.Count)
            {
                var winner = RemainingStudents[winnerIndex];
                await ProcessWinnerAsync(winner);
            }
            IsSpinning = false;
        }

        private async Task ProcessWinnerAsync(StudentGradeRow winner)
        {
            RecitationWinnerName = $"Winner: {winner.FullName}!";

            // Move in UI
            RemainingStudents.Remove(winner);
            ChosenStudents.Add(winner);

            // Save state to Database
            var db = new DatabaseService().GetConnection();
            var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == winner.StudentID).FirstOrDefaultAsync();
            
            if (rosterEntry != null)
            {
                rosterEntry.HasRecited = true;
                await db.UpdateAsync(rosterEntry);
            }
        }

        [RelayCommand]
        public async Task ResetRecitationAsync()
        {
            if (IsSpinning) return;

            var db = new DatabaseService().GetConnection();
            var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();

            // Set all to false in DB
            foreach (var r in roster)
            {
                r.HasRecited = false;
                await db.UpdateAsync(r);
            }

            // Reload UI
            await LoadRecitationData();
            // Tell the UI to physically spin the wheel back to the start!
            OnWheelResetRequested?.Invoke();
        }

        [RelayCommand]
        public async Task RestoreStudentToWheelAsync(StudentGradeRow student)
        {
            if (student == null || IsSpinning) return;

            // Move in UI
            ChosenStudents.Remove(student);
            RemainingStudents.Add(student);

            // Re-sort the remaining students alphabetically so it stays neat
            var sorted = RemainingStudents.OrderBy(s => s.StudentInfo.LastName).ToList();
            RemainingStudents.Clear();
            foreach (var s in sorted) RemainingStudents.Add(s);

            // Update Database
            var db = new DatabaseService().GetConnection();
            var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == student.StudentID).FirstOrDefaultAsync();
            
            if (rosterEntry != null)
            {
                rosterEntry.HasRecited = false;
                await db.UpdateAsync(rosterEntry);
            }
        }
    }
}