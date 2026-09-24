using System;
using System.Collections.Generic;
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
        // --- 1. STUDENT UI STATE ---
        [ObservableProperty] public partial ObservableCollection<StudentGradeRow> RemainingStudents { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<StudentGradeRow> ChosenStudents { get; set; } = new();
        [ObservableProperty] public partial string RemainingCounterText { get; set; } = "Remaining (0)";
        [ObservableProperty] public partial string ChosenCounterText { get; set; } = "Chosen (0)";

        // --- 2. QUESTION BANK STATE ---
        [ObservableProperty] public partial ObservableCollection<QuestionRowViewModel> QuestionBank { get; set; } = new();
        [ObservableProperty] public partial string TotalQuestionsCountText { get; set; } = "Total Questions: 0";
        [ObservableProperty] public partial bool AllowRepeatQuestions { get; set; } = false;
        [ObservableProperty] public partial bool ShuffleQuestions { get; set; } = true;
        
        // Tracks questions that haven't been asked yet (if Repeat is OFF)
        private List<RecitationQuestion> _availableQuestionsPool = new(); 

        // --- 3. MODAL STATE ---
        
        [ObservableProperty] public partial bool IsManageQuestionsModalOpen { get; set; } = false;
        [ObservableProperty] public partial bool IsWinnerModalOpen { get; set; } = false;
        [ObservableProperty] public partial string WinnerModalName { get; set; } = string.Empty;
        [ObservableProperty] public partial string WinnerModalQuestion { get; set; } = string.Empty;
        [ObservableProperty] public partial string WinnerModalAnswer { get; set; } = string.Empty;
        [ObservableProperty] public partial bool HasWinnerAnswer { get; set; } = false;
        [ObservableProperty] public partial bool IsAnswerRevealed { get; set; } = false;

        // Form Inputs
        [ObservableProperty] public partial string NewQuestionInput { get; set; } = string.Empty;
        [ObservableProperty] public partial string NewAnswerInput { get; set; } = string.Empty;

        // --- 4. ENGINE STATE ---
        [ObservableProperty] public partial string RecitationWinnerName { get; set; } = "Ready to Spin!";
        [ObservableProperty] public partial bool IsSpinning { get; set; } = false;
        public event Action<int>? OnSpinWheelRequested;
        public event Action? OnWheelResetRequested;

        // ==========================================
        // INITIALIZATION & LOADING
        // ==========================================
        private async Task LoadRecitationData()
        {
            var db = new DatabaseService().GetConnection();
            await db.CreateTableAsync<ClassRoster>(); 
            await db.CreateTableAsync<RecitationQuestion>(); 

            // Load Students
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

            // Load Questions
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
        }

        // Fills the "Deck" of active questions based on user settings
        private void BuildAvailableQuestionsPool()
        {
            var activeQuestions = QuestionBank.Where(q => q.IsIncluded).Select(q => q.DbModel).ToList();
            
            if (ShuffleQuestions)
            {
                var rng = new Random();
                _availableQuestionsPool = activeQuestions.OrderBy(q => rng.Next()).ToList();
            }
            else
            {
                _availableQuestionsPool = activeQuestions.OrderBy(q => q.QuestionID).ToList();
            }
        }

        // ==========================================
        // THE DRAW ENGINE
        // ==========================================
        [RelayCommand]
        public void SpinRecitation()
        {
            if (IsSpinning || !RemainingStudents.Any()) return;
            IsSpinning = true;
            RecitationWinnerName = "Spinning...";

            var random = new Random();
            int winnerIndex = random.Next(RemainingStudents.Count);
            OnSpinWheelRequested?.Invoke(winnerIndex);
        }

        [RelayCommand]
        public async Task SkipRecitationAsync()
        {
            if (IsSpinning || !RemainingStudents.Any()) return;
            var random = new Random();
            int winnerIndex = random.Next(RemainingStudents.Count);
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
            RecitationWinnerName = $"Winner: {winner.FullName}!";

            // 1. Move Student to Chosen List
            RemainingStudents.Remove(winner);
            ChosenStudents.Add(winner);
            UpdateCounters();

            var db = new DatabaseService().GetConnection();
            var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == winner.StudentID).FirstOrDefaultAsync();
            if (rosterEntry != null)
            {
                rosterEntry.HasRecited = true;
                await db.UpdateAsync(rosterEntry);
            }

            // 2. Determine Question
            RecitationQuestion? pickedQuestion = null;

            if (_availableQuestionsPool.Any())
            {
                // Grab the first question from the deck
                pickedQuestion = _availableQuestionsPool[0]; 
                
                if (!AllowRepeatQuestions)
                {
                    // Discard it if repeats are disabled
                    _availableQuestionsPool.RemoveAt(0); 
                }
                else if (ShuffleQuestions)
                {
                    // If repeating and shuffling, put it back and shuffle the whole deck again
                    BuildAvailableQuestionsPool(); 
                }
                // If repeating and NO shuffle, it naturally stays at index 0 and will be asked again!
            }

            // 3. Populate & Open Modal
            WinnerModalName = winner.FullName;
            IsAnswerRevealed = false;

            if (pickedQuestion != null)
            {
                WinnerModalQuestion = pickedQuestion.QuestionText;
                WinnerModalAnswer = pickedQuestion.AnswerText;
                HasWinnerAnswer = !string.IsNullOrWhiteSpace(pickedQuestion.AnswerText);
            }
            else
            {
                WinnerModalQuestion = "No questions available! You have run out of active questions.";
                WinnerModalAnswer = string.Empty;
                HasWinnerAnswer = false;
            }

            IsWinnerModalOpen = true;
        }

        [RelayCommand]
        public void CloseWinnerModal() => IsWinnerModalOpen = false;

        [RelayCommand]
        public void ToggleAnswerVisibility() => IsAnswerRevealed = !IsAnswerRevealed;

        
        // QUESTION MANAGER & RESET ACTIONS
        [RelayCommand]
        public async Task EditOrSaveQuestionAsync(QuestionRowViewModel qRow)
        {
            if (qRow == null) return;
            
            if (!qRow.IsEditing)
            {
                qRow.IsEditing = true; // Flips the UI to show TextBoxes
            }
            else
            {
                var db = new DatabaseService().GetConnection();
                await db.UpdateAsync(qRow.DbModel);
                
                qRow.IsEditing = false; // Flips UI back to TextBlocks
                BuildAvailableQuestionsPool(); // Update the deck with the new text
            }
        }
        [RelayCommand]
        public void OpenManageQuestionsModal() => IsManageQuestionsModalOpen = true;

        [RelayCommand]
        public void CloseManageQuestionsModal() => IsManageQuestionsModalOpen = false;

        [RelayCommand]
        public async Task SaveNewQuestionAsync()
        {
            if (string.IsNullOrWhiteSpace(NewQuestionInput)) return;

            var db = new DatabaseService().GetConnection();
            var newQuestion = new RecitationQuestion
            {
                ClassID = ClassId,
                QuestionText = NewQuestionInput.Trim(),
                AnswerText = NewAnswerInput?.Trim() ?? string.Empty,
                IsIncluded = true
            };
            
            await db.InsertAsync(newQuestion);
            
            QuestionBank.Add(new QuestionRowViewModel(newQuestion));
            TotalQuestionsCountText = $"Total Questions: {QuestionBank.Count}";
            
            NewQuestionInput = string.Empty;
            NewAnswerInput = string.Empty;
            
            BuildAvailableQuestionsPool(); 
        }

        [RelayCommand]
        public async Task DeleteQuestionAsync(QuestionRowViewModel qRow)
        {
            if (qRow == null) return;
            var db = new DatabaseService().GetConnection();
            await db.DeleteAsync(qRow.DbModel);
            
            QuestionBank.Remove(qRow);
            TotalQuestionsCountText = $"Total Questions: {QuestionBank.Count}";
            BuildAvailableQuestionsPool();
        }

        // Call this directly from the XAML CheckBox Command to instantly save the IsIncluded state
        [RelayCommand]
        public async Task ToggleQuestionIncludedAsync(QuestionRowViewModel qRow)
        {
            if (qRow == null) return;
            var db = new DatabaseService().GetConnection();
            await db.UpdateAsync(qRow.DbModel);
            BuildAvailableQuestionsPool(); 
        }

        // Refreshes the pool if you change the Global Toggles (Shuffle/Repeat)
        partial void OnShuffleQuestionsChanged(bool value) => BuildAvailableQuestionsPool();
        partial void OnAllowRepeatQuestionsChanged(bool value) => BuildAvailableQuestionsPool();

        [RelayCommand]
        public async Task ResetRecitationAsync()
        {
            if (IsSpinning) return;
            var db = new DatabaseService().GetConnection();
            var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();

            foreach (var r in roster)
            {
                r.HasRecited = false;
            }

            // High-speed transaction: Updates all 40+ rows in one massive database hit instantly!
            if (roster.Count != 0)
            {
                await db.UpdateAllAsync(roster, runInTransaction: true);
            }

            await LoadRecitationData();
            OnWheelResetRequested?.Invoke();
            ShowToastMessage?.Invoke("Class reset! All students returned to the wheel.");
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

            var db = new DatabaseService().GetConnection();
            var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == student.StudentID).FirstOrDefaultAsync();
            if (rosterEntry != null)
            {
                rosterEntry.HasRecited = false;
                await db.UpdateAsync(rosterEntry);
            }
        }

        public partial class QuestionRowViewModel : ObservableObject
        {
            public RecitationQuestion DbModel { get; }
            
            [ObservableProperty] public partial bool IsEditing { get; set; } = false;
            
            public string QuestionText { get => DbModel.QuestionText; set { DbModel.QuestionText = value; OnPropertyChanged(); } }
            public string AnswerText { get => DbModel.AnswerText; set { DbModel.AnswerText = value; OnPropertyChanged(); } }
            public bool IsIncluded { get => DbModel.IsIncluded; set { DbModel.IsIncluded = value; OnPropertyChanged(); } }

            public QuestionRowViewModel(RecitationQuestion model) { DbModel = model; }
        }
    }
}