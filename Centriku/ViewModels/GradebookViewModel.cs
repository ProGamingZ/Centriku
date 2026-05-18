using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels
{
    public partial class GradebookViewModel : ViewModelBase
    {
        [ObservableProperty] public partial int ClassId { get; set; }
        [ObservableProperty] public partial string ClassTitle { get; set; } = string.Empty;

        [ObservableProperty] public partial ObservableCollection<Assessment> ClassAssessments { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<StudentGradeRow> GradebookRows { get; set; } = new();   

        [ObservableProperty] public partial bool IsEnrolling { get; set; } = false;
        [ObservableProperty] public partial ObservableCollection<EnrollmentItemViewModel> AvailableStudents { get; set; } = new();

        [ObservableProperty] public partial bool IsAddingAssessment { get; set; } = false;
        [ObservableProperty] public partial string NewAssessmentTitle { get; set; } = string.Empty;
        [ObservableProperty] public partial double NewAssessmentMaxScore { get; set; } = 100;
        [ObservableProperty] public partial System.DateTime? NewAssessmentDate { get; set; } = System.DateTime.Now;
        private int? _editingAssessmentId = null;
        public IRelayCommand<Assessment> EditAssessmentCommand { get; }
        public IRelayCommand<Assessment> DeleteAssessmentCommand { get; }

        [ObservableProperty] public partial bool ShowLRN { get; set; } = true;
        [ObservableProperty] public partial bool ShowFirstName { get; set; } = true;
        [ObservableProperty] public partial bool ShowLastName { get; set; } = true;

        [ObservableProperty] public partial ObservableCollection<CategoryFilterViewModel> CategoryFilters { get; set; } = new();

        // A trigger number to tell the UI to instantly redraw the columns
        [ObservableProperty] public partial int GridRefreshTrigger { get; set; } = 0;
        private void TriggerGridRedraw() => GridRefreshTrigger++;

        // CommunityToolkit MVVM Magic: Auto-run these methods when the booleans change!
        partial void OnShowLRNChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
        partial void OnShowFirstNameChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }
        partial void OnShowLastNameChanged(bool value) { SaveClassSettings(); TriggerGridRedraw(); }

        private async void SaveClassSettings()
        {
            var db = new DatabaseService().GetConnection();
            var currentClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
            if (currentClass != null)
            {
                currentClass.ShowLRN = ShowLRN;
                currentClass.ShowFirstName = ShowFirstName;
                currentClass.ShowLastName = ShowLastName;
                await db.UpdateAsync(currentClass);
            }
        }

        [ObservableProperty] public partial ObservableCollection<GradingCategory> AvailableCategories { get; set; } = new();
        [ObservableProperty] public partial GradingCategory? SelectedCategory { get; set; }


        public IRelayCommand ToggleEnrollmentCommand { get; }
        public IRelayCommand SaveEnrollmentCommand { get; }
        public IRelayCommand<Student> RemoveStudentCommand { get; }
        public IRelayCommand ToggleAddAssessmentCommand { get; }
        public IRelayCommand SaveAssessmentCommand { get; }

        public GradebookViewModel()
        {
            ToggleEnrollmentCommand = new RelayCommand(ToggleEnrollment);
            SaveEnrollmentCommand = new RelayCommand(SaveEnrollment);
            RemoveStudentCommand = new RelayCommand<Student>(RemoveStudent!);

            ToggleAddAssessmentCommand = new RelayCommand(() => 
            {
                if (IsAddingAssessment) ResetAssessmentForm(); // If clicking Cancel, wipe everything clean!
                else IsAddingAssessment = true;                // If clicking Add, just open it.
            });

            SaveAssessmentCommand = new RelayCommand(SaveAssessment);

            EditAssessmentCommand = new RelayCommand<Assessment>(EditAssessment!);
            DeleteAssessmentCommand = new RelayCommand<Assessment>(DeleteAssessment!);
        }

        public async void Initialize(int classId, string classTitle)
        {
            ClassId = classId;
            ClassTitle = classTitle;
            await LoadGradebookData();
            await LoadCategories();
        }

        private async Task LoadGradebookData()
        {
            var db = new DatabaseService().GetConnection();

            // === NEW: 1. Load Class Visibility Settings ===
            var currentClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
            if (currentClass != null)
            {
                ShowLRN = currentClass.ShowLRN;
                ShowFirstName = currentClass.ShowFirstName;
                ShowLastName = currentClass.ShowLastName;
            }

            // 2. Get the Columns (Assessments)
            var assessments = await db.Table<Assessment>().Where(a => a.ClassID == ClassId).ToListAsync();
            ClassAssessments = new ObservableCollection<Assessment>(assessments);

            // === NEW: 3. Build the Category Filters for the View Menu ===
            var allFilters = assessments.Select(a => new AssessmentFilterViewModel(a, TriggerGridRedraw)).ToList();
            var grouped = allFilters.GroupBy(f => f.DbModel.Category ?? "Uncategorized");
            
            CategoryFilters.Clear();
            foreach (var group in grouped)
            {
                CategoryFilters.Add(new CategoryFilterViewModel(group.Key, group));
            }

            // 2. Get the Students in this Class
            var roster = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId).ToListAsync();
            var studentIds = roster.Select(r => r.StudentID).ToList();
            var enrolled = await db.Table<Student>().Where(s => studentIds.Contains(s.StudentID)).ToListAsync();

            // 3. Get the Scores for this Class
            var assessmentIds = assessments.Select(a => a.AssessmentID).ToList();
            var scores = await db.Table<Score>().Where(s => assessmentIds.Contains(s.AssessmentID)).ToListAsync();

            // 4. Stitch them together into Rows!
            GradebookRows.Clear();
            foreach (var student in enrolled)
            {
                var row = new StudentGradeRow(student);
                
                // Find all existing scores belonging to this specific student
                var studentScores = scores.Where(s => s.StudentID == student.StudentID).ToList();

                foreach (var assessment in ClassAssessments)
                {
                    // Check if the student already has a saved score for this column
                    var existingScore = studentScores.FirstOrDefault(s => s.AssessmentID == assessment.AssessmentID);
                    
                    if (existingScore != null)
                    {
                        // Wrap the existing score
                        row.Scores[assessment.AssessmentID] = new ScoreCellViewModel(existingScore, assessment.MaxScore);
                    }
                    else
                    {
                        var blankScore = new Score 
                        { 
                            AssessmentID = assessment.AssessmentID, 
                            StudentID = student.StudentID, 
                            PointsEarned = 0 
                        };
                        row.Scores[assessment.AssessmentID] = new ScoreCellViewModel(blankScore, assessment.MaxScore);
                    }
                }
                
                GradebookRows.Add(row);
            }
            TriggerGridRedraw();
        }

        private async void ToggleEnrollment()
        {
            IsEnrolling = !IsEnrolling;
            
            if (IsEnrolling)
            {
                var db = new DatabaseService().GetConnection();
                var allStudents = await db.Table<Student>().ToListAsync();
                var enrolledIds = GradebookRows.Select(s => s.StudentID).ToList();

                AvailableStudents.Clear();
                foreach (var s in allStudents)
                {
                    // Only show students who are NOT already enrolled in this class
                    if (s.StudentID != null && !enrolledIds.Contains(s.StudentID))
                    {
                        AvailableStudents.Add(new EnrollmentItemViewModel(s));
                    }
                }
            }
        }

        private async void SaveEnrollment()
        {
            var db = new DatabaseService().GetConnection();
            
            var selectedStudents = AvailableStudents.Where(s => s.IsSelected).ToList();

            foreach (var student in selectedStudents)
            {
                var newRosterEntry = new ClassRoster
                {
                    ClassID = ClassId, // The class we are currently viewing
                    StudentID = student.DbModel.StudentID // The student we just checked
                };
                await LoadGradebookData(); // Link them in Table 3!
            }

            IsEnrolling = false;
            await LoadGradebookData(); // Refresh the grid
        }

        private async void RemoveStudent(Student student)
        {
            if (student == null) return;
            var db = new DatabaseService().GetConnection();
            
            // Find the exact link in Table 3 and sever it
            var rosterEntry = await db.Table<ClassRoster>().Where(r => r.ClassID == ClassId && r.StudentID == student.StudentID).FirstOrDefaultAsync();
            if (rosterEntry != null)
            {
                await db.DeleteAsync(rosterEntry);
                await LoadGradebookData();
            }
        }
    
        private async Task LoadCategories()
        {
            var db = new DatabaseService().GetConnection();
            
            // 1. Get the current class to find out which Template it uses
            var currentClass = await db.Table<TeacherClass>().Where(c => c.ClassID == ClassId).FirstOrDefaultAsync();
            
            if (currentClass != null)
            {
                // 2. Fetch only the categories that belong to that specific template!
                var categories = await db.Table<GradingCategory>().Where(cat => cat.TemplateID == currentClass.GradingTemplateID).ToListAsync();
                AvailableCategories = new ObservableCollection<GradingCategory>(categories);
            }
        }
        private void EditAssessment(Assessment assessment)
        {
            if (assessment == null) return;

            _editingAssessmentId = assessment.AssessmentID;
            NewAssessmentTitle = assessment.Title ?? string.Empty;
            NewAssessmentMaxScore = assessment.MaxScore;
            NewAssessmentDate = assessment.DateGiven;
            
            // Find the matching category in the dropdown
            SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Name == assessment.Category);
            
            IsAddingAssessment = true; // Slide the form open!
        }

        private async void SaveAssessment()
        {
            if (string.IsNullOrWhiteSpace(NewAssessmentTitle) || SelectedCategory == null || NewAssessmentMaxScore <= 0) 
                return;

            var db = new DatabaseService().GetConnection();

            if (_editingAssessmentId.HasValue)
            {
                // === UPDATE MODE ===
                var assessmentToUpdate = await db.Table<Assessment>().Where(a => a.AssessmentID == _editingAssessmentId.Value).FirstOrDefaultAsync();
                assessmentToUpdate.Title = NewAssessmentTitle;
                assessmentToUpdate.Category = SelectedCategory.Name;
                assessmentToUpdate.MaxScore = NewAssessmentMaxScore;
                assessmentToUpdate.DateGiven = NewAssessmentDate ?? System.DateTime.Now;
                
                await db.UpdateAsync(assessmentToUpdate);
            }
            else
            {
                // === CREATE MODE ===
                var newAssessment = new Assessment
                {
                    ClassID = ClassId,
                    Title = NewAssessmentTitle,
                    Category = SelectedCategory.Name,
                    MaxScore = NewAssessmentMaxScore,
                    DateGiven = NewAssessmentDate ?? System.DateTime.Now
                };
                await db.InsertAsync(newAssessment);
            }
            ResetAssessmentForm();
            await LoadGradebookData(); // Refresh the grid!
        }

        private async void DeleteAssessment(Assessment assessment)
        {
            if (assessment == null) return;
            var db = new DatabaseService().GetConnection();
            
            // 1. Delete the Assessment column (Table 4)
            await db.DeleteAsync(assessment);

            // 2. Wipe all the student scores associated with this exact Quiz (Table 5)
            var scoresToDelete = await db.Table<Score>().Where(s => s.AssessmentID == assessment.AssessmentID).ToListAsync();
            foreach (var score in scoresToDelete)
            {
                await db.DeleteAsync(score);
            }

            await LoadGradebookData(); // Refresh the grid
        }
        private void ResetAssessmentForm()
        {
            _editingAssessmentId = null; // Clears the "Edit Mode" tracking ID
            NewAssessmentTitle = string.Empty;
            NewAssessmentMaxScore = 100;
            NewAssessmentDate = System.DateTime.Now;
            SelectedCategory = null;
            IsAddingAssessment = false; // Hides the form
        }
    
    
    }

    public partial class EnrollmentItemViewModel : ObservableObject
    {
        public Student DbModel { get; }
        
        [ObservableProperty] public partial bool IsSelected { get; set; } = false;

        public string FullName => $"{DbModel.LastName}, {DbModel.FirstName}";
        public string StudentID => DbModel.StudentID ?? "";

        public EnrollmentItemViewModel(Student student)
        {
            DbModel = student;
        }
    }
    public partial class StudentGradeRow : ObservableObject
    {
        public Student StudentInfo { get; }
        
        // Dictionary mapping AssessmentID -> Score object
        public System.Collections.Generic.Dictionary<int, ScoreCellViewModel> Scores { get; set; } = [];

        public string FullName => $"{StudentInfo.LastName}, {StudentInfo.FirstName}";
        public string StudentID => StudentInfo.StudentID ?? "";

        public StudentGradeRow(Student student)
        {
            StudentInfo = student;
        }
    }
    public partial class ScoreCellViewModel : ObservableObject
    {
        public Score DbModel { get; }
        public double MaxScore { get; }

        public double PointsEarned
        {
            get => DbModel.PointsEarned;
            set
            {
                // 1. THE SNAPPING LOGIC
                double finalValue = value;
                if (finalValue > MaxScore) finalValue = MaxScore; // Snap down
                if (finalValue < 0) finalValue = 0;               // Snap up

                // 2. Set the value
                if (DbModel.PointsEarned != finalValue)
                {
                    DbModel.PointsEarned = finalValue;
                    
                    // 3. Force the UI to update immediately (so they see the snap!)
                    OnPropertyChanged(); 
                    
                    // 4. Auto-Save to Database!
                    SaveScoreToDatabase(); 
                }
            }
        }

        public ScoreCellViewModel(Score score, double maxScore)
        {
            DbModel = score;
            MaxScore = maxScore;
        }

        private async void SaveScoreToDatabase()
        {
            var db = new Centriku.Services.DatabaseService().GetConnection();
            
            // If it's a new blank score, Insert it. If it exists, Update it.
            if (DbModel.ScoreID == 0) await db.InsertAsync(DbModel);
            else await db.UpdateAsync(DbModel);
        }
    }

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
                // If the teacher clicks the checkbox, save it to SQLite instantly and redraw the grid!
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

    public partial class CategoryFilterViewModel : ObservableObject
    {
        public string CategoryName { get; }
        public ObservableCollection<AssessmentFilterViewModel> Assessments { get; }

        // The Master Checkbox: If checked, it loops through and checks all children!
        public bool IsCategoryVisible
        {
            get => Assessments.Any(a => a.IsVisible); 
            set
            {
                foreach (var a in Assessments)
                {
                    a.IsVisible = value; 
                }
                OnPropertyChanged();
            }
        }

        public CategoryFilterViewModel(string name, System.Collections.Generic.IEnumerable<AssessmentFilterViewModel> assessments)
        {
            CategoryName = name;
            Assessments = new ObservableCollection<AssessmentFilterViewModel>(assessments);
            
            // Listen to children: If all quizzes are hidden, uncheck the master Category checkbox automatically
            foreach(var a in Assessments) 
            {
                a.PropertyChanged += (s,e) => 
                {
                    if (e.PropertyName == nameof(AssessmentFilterViewModel.IsVisible))
                        OnPropertyChanged(nameof(IsCategoryVisible));
                };
            }
        }
    }

}