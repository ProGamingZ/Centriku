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
        #region Groups State
        [ObservableProperty] public partial ObservableCollection<Assessment> AvailableGroupAssessments { get; set; } = new();
        [ObservableProperty] public partial Assessment? SelectedGroupAssessment { get; set; }

        [ObservableProperty] public partial ObservableCollection<GroupCardViewModel> CurrentAssessmentGroups { get; set; } = new();
        [ObservableProperty] public partial ObservableCollection<GroupCandidateStudentViewModel> UnassignedStudents { get; set; } = new();

        [ObservableProperty] public partial bool IsCreatingGroupModalOpen { get; set; } = false;
        [ObservableProperty] public partial string NewGroupNameInput { get; set; } = string.Empty;
        [ObservableProperty] public partial ObservableCollection<GroupCandidateStudentViewModel> CandidateMembers { get; set; } = new();

        partial void OnSelectedGroupAssessmentChanged(Assessment? value)
        {
            _ = LoadGroupsForSelectedAssessmentAsync();
        }
        #endregion

        public async Task LoadGroupsDataAsync()
        {
            var db = new DatabaseService().GetConnection();
            await db.CreateTableAsync<AssessmentGroup>();
            await db.CreateTableAsync<AssessmentGroupMember>();

            var groupAssessments = ClassAssessments.Where(a => a.AssessmentType == "Group/Pair").ToList();
            AvailableGroupAssessments = new ObservableCollection<Assessment>(groupAssessments);

            if (SelectedGroupAssessment == null || !AvailableGroupAssessments.Any(a => a.AssessmentID == SelectedGroupAssessment.AssessmentID))
            {
                SelectedGroupAssessment = AvailableGroupAssessments.FirstOrDefault();
            }
            else
            {
                await LoadGroupsForSelectedAssessmentAsync();
            }
        }

        public async Task LoadGroupsForSelectedAssessmentAsync()
        {
            if (SelectedGroupAssessment == null)
            {
                CurrentAssessmentGroups.Clear();
                UnassignedStudents.Clear();
                return;
            }

            var db = new DatabaseService().GetConnection();
            var groups = await db.Table<AssessmentGroup>()
                                 .Where(g => g.AssessmentID == SelectedGroupAssessment.AssessmentID)
                                 .ToListAsync();

            var allMembers = await db.Table<AssessmentGroupMember>()
                                     .Where(m => m.AssessmentID == SelectedGroupAssessment.AssessmentID)
                                     .ToListAsync();

            var enrolledStudents = GradebookRows.Select(r => r.StudentInfo).ToList();
            var assignedStudentIds = allMembers.Select(m => m.StudentID).ToHashSet();

            // 1. Identify unassigned students (eligible for 1-person solo groups or assignment)
            var unassignedList = enrolledStudents
                .Where(s => !assignedStudentIds.Contains(s.StudentID!))
                .Select(s => new GroupCandidateStudentViewModel(s))
                .ToList();
            UnassignedStudents = new ObservableCollection<GroupCandidateStudentViewModel>(unassignedList);

            // 2. Build group view models
            var groupCards = new List<GroupCardViewModel>();
            foreach (var g in groups)
            {
                var card = new GroupCardViewModel(g, SelectedGroupAssessment, SaveAndSyncGroupGradeAsync, DeleteGroupAsync);
                var members = allMembers.Where(m => m.GroupID == g.GroupID).ToList();

                foreach (var m in members)
                {
                    var student = enrolledStudents.FirstOrDefault(s => s.StudentID == m.StudentID);
                    if (student != null)
                    {
                        card.Members.Add(new GroupMemberRowViewModel(m, student, SelectedGroupAssessment, () => _ = SaveAndSyncGroupGradeAsync(card)));
                    }
                }
                card.UpdateAllMemberTotals();
                groupCards.Add(card);
            }

            CurrentAssessmentGroups = new ObservableCollection<GroupCardViewModel>(groupCards);
        }

        public async Task SaveAndSyncGroupGradeAsync(GroupCardViewModel groupCard)
        {
            if (SelectedGroupAssessment == null) return;
            var db = new DatabaseService().GetConnection();

            // 1. Persist group score
            await db.UpdateAsync(groupCard.DbModel);

            double gWeight = SelectedGroupAssessment.GroupWeight / 100.0;
            double iWeight = SelectedGroupAssessment.IndividualWeight / 100.0;

            // 2. Persist individual scores and push to main Score table
            foreach (var member in groupCard.Members)
            {
                await db.UpdateAsync(member.DbModel);

                // Math: Student Grade = (GroupScore * GroupWeight) + (IndividualScore * IndividualWeight)
                double totalEarned = Math.Round((groupCard.GroupScore * gWeight) + (member.IndividualScore * iWeight), 2);
                totalEarned = Math.Min(SelectedGroupAssessment.MaxScore, Math.Max(0, totalEarned));

                var scoreRecord = await db.Table<Score>()
                    .Where(s => s.AssessmentID == SelectedGroupAssessment.AssessmentID && s.StudentID == member.StudentID)
                    .FirstOrDefaultAsync();

                if (scoreRecord == null)
                {
                    scoreRecord = new Score
                    {
                        AssessmentID = SelectedGroupAssessment.AssessmentID,
                        StudentID = member.StudentID,
                        PointsEarned = totalEarned
                    };
                    await db.InsertAsync(scoreRecord);
                }
                else
                {
                    scoreRecord.PointsEarned = totalEarned;
                    await db.UpdateAsync(scoreRecord);
                }

                // Update in-memory Grades sheet row
                var gradebookRow = GradebookRows.FirstOrDefault(r => r.StudentID == member.StudentID);
                if (gradebookRow != null && gradebookRow.Scores.ContainsKey(SelectedGroupAssessment.AssessmentID))
                {
                    gradebookRow.Scores[SelectedGroupAssessment.AssessmentID].PointsEarned = totalEarned;
                }
            }

            groupCard.UpdateAllMemberTotals();
            RecalculateFinalGrades();
        }

        [RelayCommand]
        public void OpenCreateGroupModal()
        {
            if (SelectedGroupAssessment == null)
            {
                ShowToastMessage?.Invoke("Select a group assessment first.");
                return;
            }

            CandidateMembers = new ObservableCollection<GroupCandidateStudentViewModel>(
                UnassignedStudents.Select(s => new GroupCandidateStudentViewModel(s.Student))
            );
            NewGroupNameInput = $"Group {CurrentAssessmentGroups.Count + 1}";
            IsCreatingGroupModalOpen = true;
        }

        [RelayCommand]
        public async Task AddSoloGroupAsync(GroupCandidateStudentViewModel candidate)
        {
            if (SelectedGroupAssessment == null || candidate == null) return;
            var db = new DatabaseService().GetConnection();

            var newGroup = new AssessmentGroup
            {
                AssessmentID = SelectedGroupAssessment.AssessmentID,
                ClassID = ClassId,
                GroupName = $"Solo: {candidate.Student.LastName}",
                GroupScore = 0
            };
            await db.InsertAsync(newGroup);

            var member = new AssessmentGroupMember
            {
                GroupID = newGroup.GroupID,
                AssessmentID = SelectedGroupAssessment.AssessmentID,
                StudentID = candidate.Student.StudentID!,
                IndividualScore = 0
            };
            await db.InsertAsync(member);

            await LoadGroupsForSelectedAssessmentAsync();
            ShowToastMessage?.Invoke($"Created 1-person group for {candidate.Student.LastName}.");
        }

        [RelayCommand]
        public async Task ConfirmCreateGroupAsync()
        {
            if (SelectedGroupAssessment == null || string.IsNullOrWhiteSpace(NewGroupNameInput)) return;
            var selectedStudents = CandidateMembers.Where(m => m.IsSelected).ToList();

            if (!selectedStudents.Any())
            {
                ShowToastMessage?.Invoke("Please check at least one student for this group.");
                return;
            }

            var db = new DatabaseService().GetConnection();
            var newGroup = new AssessmentGroup
            {
                AssessmentID = SelectedGroupAssessment.AssessmentID,
                ClassID = ClassId,
                GroupName = NewGroupNameInput.Trim(),
                GroupScore = 0
            };
            await db.InsertAsync(newGroup);

            foreach (var student in selectedStudents)
            {
                var member = new AssessmentGroupMember
                {
                    GroupID = newGroup.GroupID,
                    AssessmentID = SelectedGroupAssessment.AssessmentID,
                    StudentID = student.Student.StudentID!,
                    IndividualScore = 0
                };
                await db.InsertAsync(member);
            }

            IsCreatingGroupModalOpen = false;
            await LoadGroupsForSelectedAssessmentAsync();
        }

        [RelayCommand]
        public void CancelCreateGroup() => IsCreatingGroupModalOpen = false;

        public async Task DeleteGroupAsync(GroupCardViewModel groupCard)
        {
            var db = new DatabaseService().GetConnection();
            await db.Table<AssessmentGroupMember>().Where(m => m.GroupID == groupCard.DbModel.GroupID).DeleteAsync();
            await db.DeleteAsync(groupCard.DbModel);
            await LoadGroupsForSelectedAssessmentAsync();
            RecalculateFinalGrades();
        }
    }

    // Helper item view models for UI binding
    public partial class GroupCandidateStudentViewModel(Student student) : ObservableObject
    {
        public Student Student { get; } = student;
        public string StudentID => Student.StudentID ?? "";
        public string FullName => $"{Student.LastName}, {Student.FirstName}";
        [ObservableProperty] public partial bool IsSelected { get; set; } = false;
    }

    public partial class GroupMemberRowViewModel : ObservableObject
    {
        public AssessmentGroupMember DbModel { get; }
        public Student StudentInfo { get; }
        private readonly Assessment _assessment;
        private readonly System.Action _onScoreChanged;

        public string StudentID => StudentInfo.StudentID ?? "";
        public string FullName => $"{StudentInfo.LastName}, {StudentInfo.FirstName}";

        public double IndividualScore
        {
            get => DbModel.IndividualScore;
            set
            {
                double val = Math.Min(_assessment.MaxScore, Math.Max(0, value));
                if (DbModel.IndividualScore != val)
                {
                    DbModel.IndividualScore = val;
                    OnPropertyChanged();
                    _onScoreChanged?.Invoke();
                }
            }
        }

        [ObservableProperty] public partial double TotalComputedGrade { get; set; }

        public GroupMemberRowViewModel(AssessmentGroupMember member, Student student, Assessment assessment, System.Action onScoreChanged)
        {
            DbModel = member;
            StudentInfo = student;
            _assessment = assessment;
            _onScoreChanged = onScoreChanged;
        }

        public void CalculateTotal(double groupScore)
        {
            double gWeight = _assessment.GroupWeight / 100.0;
            double iWeight = _assessment.IndividualWeight / 100.0;
            TotalComputedGrade = Math.Round((groupScore * gWeight) + (IndividualScore * iWeight), 2);
        }
    }

    public partial class GroupCardViewModel : ObservableObject
    {
        public AssessmentGroup DbModel { get; }
        private readonly Assessment _assessment;
        private readonly Func<GroupCardViewModel, Task> _onSave;
        private readonly Func<GroupCardViewModel, Task> _onDelete;

        public string GroupName => DbModel.GroupName;
        public double MaxScore => _assessment.MaxScore;

        public double GroupScore
        {
            get => DbModel.GroupScore;
            set
            {
                double val = Math.Min(_assessment.MaxScore, Math.Max(0, value));
                if (DbModel.GroupScore != val)
                {
                    DbModel.GroupScore = val;
                    OnPropertyChanged();
                    UpdateAllMemberTotals();
                    _ = _onSave(this);
                }
            }
        }

        [ObservableProperty] public partial ObservableCollection<GroupMemberRowViewModel> Members { get; set; } = new();

        public IRelayCommand DeleteGroupCommand { get; }

        public GroupCardViewModel(AssessmentGroup group, Assessment assessment, Func<GroupCardViewModel, Task> onSave, Func<GroupCardViewModel, Task> onDelete)
        {
            DbModel = group;
            _assessment = assessment;
            _onSave = onSave;
            _onDelete = onDelete;
            DeleteGroupCommand = new RelayCommand(async () => await _onDelete(this));
        }

        public void UpdateAllMemberTotals()
        {
            foreach (var m in Members)
            {
                m.CalculateTotal(GroupScore);
            }
        }
    }
}