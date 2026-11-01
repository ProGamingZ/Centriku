using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;
using Centriku.ViewModels.Modals;

namespace Centriku.ViewModels
{
    public partial class GradebookViewModel
    {
        // --- 1. STUDENT UI STATE ---
        [ObservableProperty] public partial ObservableCollection<StudentGradeRow> RemainingStudents { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<StudentGradeRow> ChosenStudents { get; set; } = new();
        [ObservableProperty] public partial string RemainingCounterText { get; set; } = "Remaining (0)";
        [ObservableProperty] public partial string ChosenCounterText { get; set; } = "Chosen (0)";
        public bool HasNoRemainingStudents => RemainingStudents == null || RemainingStudents.Count == 0;
        public bool HasNoChosenStudents => ChosenStudents == null || ChosenStudents.Count == 0;

        // --- 2. QUESTION BANK STATE ---
        [ObservableProperty] public partial ObservableCollection<QuestionRowViewModel> QuestionBank { get; set; } = new();
        [ObservableProperty] public partial string TotalQuestionsCountText { get; set; } = "Total Questions: 0";
        [ObservableProperty] public partial bool AllowRepeatQuestions { get; set; } = false;
        [ObservableProperty] public partial bool ShuffleQuestions { get; set; } = true;
        
        private List<RecitationQuestion> _availableQuestionsPool = new(); 

        // --- 3. MODAL STATE ---
        [ObservableProperty] public partial RecitationWinnerModalViewModel? WinnerModal { get; set; }
        [ObservableProperty] public partial ManageQuestionsModalViewModel? ManageQuestionsModal { get; set; }
        
        [ObservableProperty] public partial bool IsWinnerModalOpen { get; set; } = false;
        [ObservableProperty] public partial bool IsManageQuestionsModalOpen { get; set; } = false;

        [ObservableProperty] public partial string NewQuestionInput { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewAnswerInput { get; set; } = string.Empty;

        // --- 4. ENGINE STATE ---
        [ObservableProperty] public partial string RecitationWinnerName { get; set; } = "Ready to Spin!";
        [ObservableProperty] public partial bool IsSpinning { get; set; } = false;
        public event Action<int>? OnSpinWheelRequested;
        public event Action? OnWheelResetRequested;

        // INITIALIZATION & LOADING
        private async Task LoadRecitationData()
        {
            var db = _dbService.GetConnection();
            await db.CreateTableAsync<ClassRoster>(); 
            await db.CreateTableAsync<RecitationQuestion>(); 

            var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
            var remaining = new List<StudentGradeRow>();
            var chosen = new List<StudentGradeRow>();

            foreach (var studentRow in GradebookRows)
            {
                var rosterEntry = roster.FirstOrDefault(r => r.StudentID == studentRow.StudentID);
                if (rosterEntry != null && rosterEntry.HasRecited) chosen.Add(studentRow);
                else remaining.Add(studentRow);
            }

            RemainingStudents = new ObservableCollection<StudentGradeRow>(remaining);
            ChosenStudents = new ObservableCollection<StudentGradeRow>(chosen);
            UpdateCounters();

            var questions = await db.Table<RecitationQuestion>().Where(q => q.ClassID == ClassId).ToListAsync();
            QuestionBank = new ObservableCollection<QuestionRowViewModel>(questions.Select(q => new QuestionRowViewModel(q)));
            
            TotalQuestionsCountText = $"Total Questions: {QuestionBank.Count}";
            RecitationWinnerName = "Ready to Spin!";
            
            BuildAvailableQuestionsPool();
        }

        private void UpdateCounters()
        {
            RemainingCounterText = $"Remaining ({RemainingStudents.Count})";
            ChosenCounterText = $"Chosen ({ChosenStudents.Count})";
            OnPropertyChanged(nameof(HasNoRemainingStudents));
            OnPropertyChanged(nameof(HasNoChosenStudents));
        }

        private void BuildAvailableQuestionsPool()
        {
            TotalQuestionsCountText = $"Total Questions: {QuestionBank.Count}";
            var activeQuestions = QuestionBank.Where(q => q.IsIncluded).Select(q => q.DbModel).ToList();
            
            if (ShuffleQuestions)
            {
                _availableQuestionsPool = activeQuestions.OrderBy(_ => Random.Shared.Next()).ToList();
            }
            else
            {
                _availableQuestionsPool = activeQuestions.OrderBy(q => q.QuestionID).ToList();
            }
        }

        // THE DRAW ENGINE
        [RelayCommand]
        public void SpinRecitation()
        {
            if (IsSpinning) return;
            if (!RemainingStudents.Any())
            {
                ShowToastMessage?.Invoke("All students have recited! Click 'Reset Class' to start a new round.");
                return;
            }

            IsSpinning = true;
            RecitationWinnerName = "Spinning...";

            int winnerIndex = Random.Shared.Next(RemainingStudents.Count);
            OnSpinWheelRequested?.Invoke(winnerIndex);
        }

        [RelayCommand]
        public async Task SkipRecitationAsync()
        {
            if (IsSpinning) return;
            if (!RemainingStudents.Any())
            {
                ShowToastMessage?.Invoke("All students have recited! Click 'Reset Class' to start a new round.");
                return;
            }

            int winnerIndex = Random.Shared.Next(RemainingStudents.Count);
            await ProcessWinnerAsync(RemainingStudents[winnerIndex]);
        }

        public async Task WheelAnimationCompletedAsync(int winnerIndex)
        {
            if (winnerIndex >= 0 && winnerIndex < RemainingStudents.Count)
            {
                await ProcessWinnerAsync(RemainingStudents[winnerIndex]);
            }
            IsSpinning = false;
        }

        private async Task ProcessWinnerAsync(StudentGradeRow winner)
        {
            RecitationWinnerName = $"Selected: {winner.FullName}";

            RemainingStudents.Remove(winner);
            ChosenStudents.Add(winner);
            UpdateCounters();

            var db = _dbService.GetConnection();
            var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == winner.StudentID).FirstOrDefaultAsync();
            if (rosterEntry != null)
            {
                rosterEntry.HasRecited = true;
                await db.UpdateAsync(rosterEntry);
            }

            RecitationQuestion? pickedQuestion = null;

            if (_availableQuestionsPool.Any())
            {
                pickedQuestion = _availableQuestionsPool[0]; 
                
                if (!AllowRepeatQuestions)
                {
                    _availableQuestionsPool.RemoveAt(0); 
                }
                else if (ShuffleQuestions)
                {
                    BuildAvailableQuestionsPool(); 
                }
            }

            string questionText = pickedQuestion != null ? pickedQuestion.QuestionText : "No questions available! You have run out of active questions.";
            string answerText = pickedQuestion != null ? pickedQuestion.AnswerText : string.Empty;

            WinnerModal = new RecitationWinnerModalViewModel(
                winner.FullName, 
                questionText, 
                answerText, 
                () => { IsWinnerModalOpen = false; WinnerModal = null; }
            );

            IsWinnerModalOpen = true;
        }
        
        // QUESTION MANAGER & RESET ACTIONS
        [RelayCommand]
        public void OpenManageQuestionsModal()
        {
            ManageQuestionsModal = new ManageQuestionsModalViewModel(
                _dbService,
                ClassId, 
                QuestionBank, 
                BuildAvailableQuestionsPool, 
                () => { IsManageQuestionsModalOpen = false; ManageQuestionsModal = null; }
            );
            IsManageQuestionsModalOpen = true;
        }

        [RelayCommand]
        public async Task ToggleQuestionIncludedAsync(QuestionRowViewModel qRow)
        {
            if (qRow == null) return;
            var db = _dbService.GetConnection();
            await db.UpdateAsync(qRow.DbModel);
            BuildAvailableQuestionsPool(); 
        }

        partial void OnShuffleQuestionsChanged(bool value) => BuildAvailableQuestionsPool();
        partial void OnAllowRepeatQuestionsChanged(bool value) => BuildAvailableQuestionsPool();

        [RelayCommand]
        public async Task ResetRecitationAsync()
        {
            if (IsSpinning) return;
            
            try 
            {
                var db = _dbService.GetConnection();
                await db.CreateTableAsync<ClassRoster>();
                await db.CreateTableAsync<RecitationQuestion>();

                // Single atomic SQL statement resets all students in < 1ms without savepoint locks
                await db.ExecuteAsync("UPDATE ClassRoster SET HasRecited = 0 WHERE ClassID = ?", ClassId);

                // Reloads RemainingStudents, ChosenStudents, and QuestionBank from DB (< 1ms)
                await LoadRecitationData();

                OnWheelResetRequested?.Invoke();
                ShowToastMessage?.Invoke("Class reset! All students returned to the wheel.");
            }
            catch (Exception ex)
            {
                ShowToastMessage?.Invoke($"Error resetting recitation: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task RestoreStudentToWheelAsync(StudentGradeRow student)
        {
            if (student == null || IsSpinning) return;

            ChosenStudents.Remove(student);
            RemainingStudents.Add(student);

            var sorted = RemainingStudents.OrderBy(s => s.StudentInfo.LastName).ToList();
            RemainingStudents.Clear();
            foreach (var s in sorted) RemainingStudents.Add(s);
            UpdateCounters();

            var db = _dbService.GetConnection();
            var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == student.StudentID).FirstOrDefaultAsync();
            if (rosterEntry != null)
            {
                rosterEntry.HasRecited = false;
                await db.UpdateAsync(rosterEntry);
            }
        }
    }
}