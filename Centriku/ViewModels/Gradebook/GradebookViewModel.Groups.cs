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

        [ObservableProperty] public partial bool IsDeleteGroupModalOpen { get; set; } = false;
        [ObservableProperty] public partial string DeleteGroupModalMessage { get; set; } = string.Empty;
        private GroupCardViewModel? _groupToDelete;
        [ObservableProperty] public partial bool IsEditingGroupModalOpen { get; set; } = false;
        [ObservableProperty] public partial string EditGroupNameInput { get; set; } = string.Empty;
        [ObservableProperty] public partial ObservableCollection<GroupCandidateStudentViewModel> EditCandidateMembers { get; set; } = new();
        private GroupCardViewModel? _groupToEdit;
        [ObservableProperty] public partial bool IsCopyGroupsModalOpen { get; set; } = false;
        [ObservableProperty] public partial ObservableCollection<Assessment> CopyFromAssessments { get; set; } = new();
        [ObservableProperty] public partial Assessment? SelectedCopyFromAssessment { get; set; }

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
            var previousSelectedId = SelectedGroupAssessment?.AssessmentID;
            
            AvailableGroupAssessments = new ObservableCollection<Assessment>(groupAssessments);

            if (previousSelectedId.HasValue)
            {
                // Find the matching object in the NEW list and re-select it
                SelectedGroupAssessment = AvailableGroupAssessments.FirstOrDefault(a => a.AssessmentID == previousSelectedId.Value) 
                                          ?? AvailableGroupAssessments.FirstOrDefault();
            }
            else
            {
                SelectedGroupAssessment = AvailableGroupAssessments.FirstOrDefault();
            }
            
            await LoadGroupsForSelectedAssessmentAsync();
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
                        card.Members.Add(new GroupMemberRowViewModel(m, student, SelectedGroupAssessment, card, () => _ = SaveAndSyncGroupGradeAsync(card)));
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

            var membersToUpdate = new List<AssessmentGroupMember>();
            var scoresToInsert = new List<Score>();
            var scoresToUpdate = new List<Score>();

            // Fetch all existing scores for this group in ONE query instead of inside a loop
            var memberIds = groupCard.Members.Select(m => m.StudentID).ToList();
            var existingScores = await db.Table<Score>()
                .Where(s => s.AssessmentID == SelectedGroupAssessment.AssessmentID && memberIds.Contains(s.StudentID))
                .ToListAsync();

            // 2. Prepare all the data in memory instantly
            foreach (var member in groupCard.Members)
            {
                membersToUpdate.Add(member.DbModel);

                // Math: Student Grade = (GroupScore * GroupWeight) + (IndividualScore * IndividualWeight)
                double totalEarned = Math.Round((groupCard.GroupScore * gWeight) + (member.IndividualScore * iWeight), 2);
                totalEarned = Math.Min(SelectedGroupAssessment.MaxScore, Math.Max(0, totalEarned));

                var scoreRecord = existingScores.FirstOrDefault(s => s.StudentID == member.StudentID);

                if (scoreRecord == null)
                {
                    scoresToInsert.Add(new Score
                    {
                        AssessmentID = SelectedGroupAssessment.AssessmentID,
                        StudentID = member.StudentID,
                        PointsEarned = totalEarned
                    });
                }
                else
                {
                    scoreRecord.PointsEarned = totalEarned;
                    scoresToUpdate.Add(scoreRecord);
                }

                // Update in-memory Grades sheet row instantly for UI responsiveness
                var gradebookRow = GradebookRows.FirstOrDefault(r => r.StudentID == member.StudentID);
                if (gradebookRow != null && gradebookRow.Scores.ContainsKey(SelectedGroupAssessment.AssessmentID))
                {
                    gradebookRow.Scores[SelectedGroupAssessment.AssessmentID].PointsEarned = totalEarned;
                }
            }

            // 3. Execute all DB operations in BULK (Instantaneous)
            await db.UpdateAllAsync(membersToUpdate);
            if (scoresToInsert.Any()) await db.InsertAllAsync(scoresToInsert, runInTransaction: true);
            if (scoresToUpdate.Any()) await db.UpdateAllAsync(scoresToUpdate, runInTransaction: true);

            groupCard.UpdateAllMemberTotals();

            // 4. Run the heavy final grade recalculation in the background so the UI doesn't freeze!
            _ = Task.Run(() => 
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => RecalculateFinalGrades());
            });
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

            // NEW: Ensure unique group name for solo students with the same last name
            string baseName = $"Solo: {candidate.Student.LastName}";
            string newGroupName = baseName;
            int counter = 1;
            while (CurrentAssessmentGroups.Any(g => string.Equals(g.GroupName, newGroupName, StringComparison.OrdinalIgnoreCase)))
            {
                newGroupName = $"{baseName} ({counter})";
                counter++;
            }

            var newGroup = new AssessmentGroup
            {
                AssessmentID = SelectedGroupAssessment.AssessmentID,
                ClassID = ClassId,
                GroupName = newGroupName,
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
            
            var db = new DatabaseService().GetConnection();
            string trimmedName = NewGroupNameInput.Trim();

            // NEW: Check if the group name already exists in this assessment
            var existingGroup = await db.Table<AssessmentGroup>()
                .Where(g => g.AssessmentID == SelectedGroupAssessment.AssessmentID && g.GroupName == trimmedName)
                .FirstOrDefaultAsync();

            if (existingGroup != null)
            {
                ShowToastMessage?.Invoke($"A group named '{trimmedName}' already exists.");
                return; // Stops here, leaves the modal open so they can change the name
            }

            var selectedStudents = CandidateMembers.Where(m => m.IsSelected).ToList();

            if (!selectedStudents.Any())
            {
                ShowToastMessage?.Invoke("Please check at least one student for this group.");
                return;
            }

            var newGroup = new AssessmentGroup
            {
                AssessmentID = SelectedGroupAssessment.AssessmentID,
                ClassID = ClassId,
                GroupName = trimmedName,
                GroupScore = 0
            };
            await db.InsertAsync(newGroup);

            var newMembers = new List<AssessmentGroupMember>();
            foreach (var student in selectedStudents)
            {
                newMembers.Add(new AssessmentGroupMember
                {
                    GroupID = newGroup.GroupID,
                    AssessmentID = SelectedGroupAssessment.AssessmentID,
                    StudentID = student.Student.StudentID!,
                    IndividualScore = 0
                });
            }
            await db.InsertAllAsync(newMembers);

            IsCreatingGroupModalOpen = false;
            await LoadGroupsForSelectedAssessmentAsync();
            ShowToastMessage?.Invoke($"Group '{newGroup.GroupName}' created successfully with {newMembers.Count} members.");
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
    
        [RelayCommand]
        public void PromptDeleteGroup(GroupCardViewModel groupCard)
        {
            if (groupCard == null) return;
            _groupToDelete = groupCard;
            DeleteGroupModalMessage = $"Are you sure you want to delete '{groupCard.GroupName}'?\n\nThis will remove the group and permanently erase all scores for its members in this assessment.";
            IsDeleteGroupModalOpen = true;
        }

        [RelayCommand]
        public async Task ConfirmDeleteGroupAsync()
        {
            if (_groupToDelete == null) return;
            var db = new DatabaseService().GetConnection();
            
            // Delete members first, then the group
            await db.Table<AssessmentGroupMember>().Where(m => m.GroupID == _groupToDelete.DbModel.GroupID).DeleteAsync();
            await db.DeleteAsync(_groupToDelete.DbModel);
            
            IsDeleteGroupModalOpen = false;
            _groupToDelete = null;
            
            await LoadGroupsForSelectedAssessmentAsync();
            RecalculateFinalGrades();
            ShowToastMessage?.Invoke("Group deleted successfully.");
        }

        [RelayCommand]
        public void CancelDeleteGroup()
        {
            IsDeleteGroupModalOpen = false;
            _groupToDelete = null;
        }

        [RelayCommand]
        public void OpenEditGroupModal(GroupCardViewModel groupCard)
        {
            if (groupCard == null) return;
            _groupToEdit = groupCard;
            EditGroupNameInput = groupCard.GroupName;

            var candidates = new List<GroupCandidateStudentViewModel>();
            
            // 1. Add current members (Checked)
            foreach (var member in groupCard.Members)
            {
                candidates.Add(new GroupCandidateStudentViewModel(member.StudentInfo) { IsSelected = true });
            }

            // 2. Add unassigned students (Unchecked)
            foreach (var unassigned in UnassignedStudents)
            {
                candidates.Add(new GroupCandidateStudentViewModel(unassigned.Student) { IsSelected = false });
            }

            // Sort alphabetically for convenience
            EditCandidateMembers = new ObservableCollection<GroupCandidateStudentViewModel>(candidates.OrderBy(c => c.Student.LastName));
            
            IsEditingGroupModalOpen = true;
        }

        [RelayCommand]
        public async Task ConfirmEditGroupAsync()
        {
            if (_groupToEdit == null || string.IsNullOrWhiteSpace(EditGroupNameInput) || SelectedGroupAssessment == null) return;
            
            var db = new DatabaseService().GetConnection();
            string trimmedName = EditGroupNameInput.Trim();

            // NEW: Check if the new name exists on a DIFFERENT group
            var existingGroup = await db.Table<AssessmentGroup>()
                .Where(g => g.AssessmentID == SelectedGroupAssessment.AssessmentID && g.GroupName == trimmedName && g.GroupID != _groupToEdit.DbModel.GroupID)
                .FirstOrDefaultAsync();

            if (existingGroup != null)
            {
                ShowToastMessage?.Invoke($"A group named '{trimmedName}' already exists.");
                return;
            }

            var selectedStudents = EditCandidateMembers.Where(m => m.IsSelected).ToList();
            if (!selectedStudents.Any())
            {
                ShowToastMessage?.Invoke("A group must have at least one member.");
                return;
            }
            
            // 1. Update Group Name
            _groupToEdit.DbModel.GroupName = trimmedName;
            await db.UpdateAsync(_groupToEdit.DbModel);

            var existingMemberIds = _groupToEdit.Members.Select(m => m.StudentID).ToList();
            var newSelectedIds = selectedStudents.Select(s => s.StudentID).ToList();

            // 2. Find members to REMOVE
            var membersToRemove = _groupToEdit.Members.Where(m => !newSelectedIds.Contains(m.StudentID)).ToList();
            foreach (var m in membersToRemove)
            {
                await db.DeleteAsync(m.DbModel);
                var score = await db.Table<Score>().Where(s => s.AssessmentID == SelectedGroupAssessment.AssessmentID && s.StudentID == m.StudentID).FirstOrDefaultAsync();
                if (score != null) await db.DeleteAsync(score);
            }

            // 3. Find members to ADD
            var membersToAdd = selectedStudents.Where(s => !existingMemberIds.Contains(s.StudentID)).ToList();
            foreach (var s in membersToAdd)
            {
                var newMember = new AssessmentGroupMember
                {
                    GroupID = _groupToEdit.DbModel.GroupID,
                    AssessmentID = SelectedGroupAssessment.AssessmentID,
                    StudentID = s.StudentID,
                    IndividualScore = 0
                };
                await db.InsertAsync(newMember);
            }

            IsEditingGroupModalOpen = false;
            _groupToEdit = null;
            
            await LoadGroupsForSelectedAssessmentAsync();
            RecalculateFinalGrades();
            ShowToastMessage?.Invoke("Group updated successfully.");
        }

        [RelayCommand]
        public void CancelEditGroup()
        {
            IsEditingGroupModalOpen = false;
            _groupToEdit = null;
        }
        [RelayCommand]
        public void OpenCopyGroupsModal()
        {
            if (SelectedGroupAssessment == null) return;
            
            // Find all group assessments EXCEPT the one currently selected
            var otherAssessments = AvailableGroupAssessments.Where(a => a.AssessmentID != SelectedGroupAssessment.AssessmentID).ToList();
            
            if (!otherAssessments.Any())
            {
                ShowToastMessage?.Invoke("There are no other group assessments to copy from.");
                return;
            }

            CopyFromAssessments = new ObservableCollection<Assessment>(otherAssessments);
            SelectedCopyFromAssessment = CopyFromAssessments.FirstOrDefault();
            IsCopyGroupsModalOpen = true;
        }
        [RelayCommand]
        public async Task ConfirmCopyGroupsAsync()
        {
            if (SelectedGroupAssessment == null || SelectedCopyFromAssessment == null) return;
            
            var db = new DatabaseService().GetConnection();
            
            // 1. Fetch data from the SOURCE assessment
            var sourceGroups = await db.Table<AssessmentGroup>().Where(g => g.AssessmentID == SelectedCopyFromAssessment.AssessmentID).ToListAsync();
            var sourceMembers = await db.Table<AssessmentGroupMember>().Where(m => m.AssessmentID == SelectedCopyFromAssessment.AssessmentID).ToListAsync();

            if (!sourceGroups.Any())
            {
                ShowToastMessage?.Invoke("The selected assessment has no groups to copy.");
                return;
            }

            // 2. Fetch data from the DESTINATION assessment to enforce uniqueness
            var destGroups = await db.Table<AssessmentGroup>().Where(g => g.AssessmentID == SelectedGroupAssessment.AssessmentID).ToListAsync();
            var destMembers = await db.Table<AssessmentGroupMember>().Where(m => m.AssessmentID == SelectedGroupAssessment.AssessmentID).ToListAsync();

            var existingDestGroupNames = destGroups.Select(g => g.GroupName?.ToLower()).ToHashSet();
            var alreadyAssignedStudentIds = destMembers.Select(m => m.StudentID).ToHashSet();

            var newMembersToInsert = new List<AssessmentGroupMember>();
            int groupsCopied = 0;

            // 3. Clone and Filter
            foreach (var srcGroup in sourceGroups)
            {
                var srcGroupMembers = sourceMembers.Where(m => m.GroupID == srcGroup.GroupID).ToList();
                
                // RESTRICTION: Only copy students who aren't already in a group in the destination!
                var validMembersToCopy = srcGroupMembers.Where(m => !alreadyAssignedStudentIds.Contains(m.StudentID)).ToList();

                // If all members of this group are already assigned elsewhere, completely skip copying this group
                if (!validMembersToCopy.Any()) continue;

                // RESTRICTION: Auto-rename the group if the name already exists
                string newGroupName = srcGroup.GroupName ?? "Unnamed Group";
                int copyCounter = 1;
                while (existingDestGroupNames.Contains(newGroupName.ToLower()))
                {
                    newGroupName = $"{srcGroup.GroupName} ({copyCounter})";
                    copyCounter++;
                }
                existingDestGroupNames.Add(newGroupName.ToLower()); // Reserve the name so the next iteration doesn't use it

                var newGroup = new AssessmentGroup
                {
                    AssessmentID = SelectedGroupAssessment.AssessmentID,
                    ClassID = ClassId,
                    GroupName = newGroupName,
                    GroupScore = 0 
                };
                await db.InsertAsync(newGroup);
                groupsCopied++;

                foreach(var m in validMembersToCopy)
                {
                    newMembersToInsert.Add(new AssessmentGroupMember
                    {
                        GroupID = newGroup.GroupID,
                        AssessmentID = SelectedGroupAssessment.AssessmentID,
                        StudentID = m.StudentID,
                        IndividualScore = 0 
                    });
                }
            }

            if (newMembersToInsert.Any())
            {
                await db.InsertAllAsync(newMembersToInsert);
            }

            IsCopyGroupsModalOpen = false;
            await LoadGroupsForSelectedAssessmentAsync();

            if (groupsCopied == 0)
            {
                ShowToastMessage?.Invoke("No groups copied. All students from the source are already assigned to groups here.");
            }
            else
            {
                ShowToastMessage?.Invoke($"Successfully copied {groupsCopied} groups.");
            }
        }
        [RelayCommand]
        public void CancelCopyGroups() => IsCopyGroupsModalOpen = false;

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
        private readonly GroupCardViewModel _parentCard;
        private readonly System.Action _onScoreChanged;

        public string StudentID => StudentInfo.StudentID ?? "";
        public string FullName => $"{StudentInfo.LastName}, {StudentInfo.FirstName}";

        public double IndividualScore => DbModel.IndividualScore;

        // Safely handles empty inputs and letters, and calculates math instantly
        public string IndividualScoreDisplay
        {
            get => IndividualScore.ToString("0.##");
            set
            {
                if (string.IsNullOrWhiteSpace(value ?? "")) {
                    SetIndividualScore(0);
                } else if (double.TryParse(value, out double numericValue)) {
                    SetIndividualScore(numericValue);
                }
                
                // ALWAYS tell the UI to refresh its text to match the mathematically clamped value.
                // This instantly erases letters or numbers over the MaxScore!
                OnPropertyChanged(nameof(IndividualScoreDisplay));
            }
        }

        [ObservableProperty] public partial double TotalComputedGrade { get; set; }

        public GroupMemberRowViewModel(AssessmentGroupMember member, Student student, Assessment assessment, GroupCardViewModel parentCard, System.Action onScoreChanged)
        {
            DbModel = member;
            StudentInfo = student;
            _assessment = assessment;
            _parentCard = parentCard;
            _onScoreChanged = onScoreChanged;
        }

        private void SetIndividualScore(double value)
        {
            double val = Math.Min(_assessment.MaxScore, Math.Max(0, value));
            if (DbModel.IndividualScore != val)
            {
                DbModel.IndividualScore = val;
                OnPropertyChanged(nameof(IndividualScore));
                OnPropertyChanged(nameof(IndividualScoreDisplay));
                
                // INSTANT CALCULATION: Run the math immediately in memory before the background save happens
                CalculateTotal(_parentCard.GroupScore); 
                
                _onScoreChanged?.Invoke();
            }
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

        public string GroupName => DbModel.GroupName;
        public double MaxScore => _assessment.MaxScore;

        public double GroupScore => DbModel.GroupScore;

        // Safely handles empty inputs and letters, and calculates math instantly
        public string GroupScoreDisplay
        {
            get => GroupScore.ToString("0.##");
            set
            {
                if (string.IsNullOrWhiteSpace(value ?? "")) {
                    SetGroupScore(0);
                } else if (double.TryParse(value, out double numericValue)) {
                    SetGroupScore(numericValue);
                }
                
                // ALWAYS tell the UI to refresh its text to match the mathematically clamped value.
                // This instantly erases letters or numbers over the MaxScore!
                OnPropertyChanged(nameof(GroupScoreDisplay));
            }
        }

        [ObservableProperty] public partial ObservableCollection<GroupMemberRowViewModel> Members { get; set; } = new();

        public GroupCardViewModel(AssessmentGroup group, Assessment assessment, Func<GroupCardViewModel, Task> onSave, Func<GroupCardViewModel, Task> onDelete)
        {
            DbModel = group;
            _assessment = assessment;
            _onSave = onSave;
        }

        private void SetGroupScore(double value)
        {
            double val = Math.Min(_assessment.MaxScore, Math.Max(0, value));
            if (DbModel.GroupScore != val)
            {
                DbModel.GroupScore = val;
                OnPropertyChanged(nameof(GroupScore));
                OnPropertyChanged(nameof(GroupScoreDisplay));
                
                // INSTANT CALCULATION: Update all members' math immediately in memory
                UpdateAllMemberTotals(); 
                
                _ = _onSave(this);
            }
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