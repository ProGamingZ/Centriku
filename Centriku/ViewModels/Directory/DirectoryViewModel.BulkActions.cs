using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.ViewModels.Modals;

namespace Centriku.ViewModels
{
    public partial class DirectoryViewModel
    {
        [ObservableProperty] public partial ObservableCollection<StudentRowViewModel> DisplayedArchivedStudents { get; set; } = new();
        [ObservableProperty] public partial string ArchiveSearchQuery { get; set; } = string.Empty;
        [ObservableProperty] public partial string DynamicArchiveCounter { get; set; } = "Showing: 0 students";

        partial void OnArchiveSearchQueryChanged(string value) => UpdateDisplayedArchivedStudents();

        // Archive Column Visibility
        [ObservableProperty] public partial bool ShowArchiveStudentIdColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveLastNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveFirstNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveMiddleNameColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveSuffixColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveGenderColumn { get; set; } = false;
        [ObservableProperty] public partial bool ShowArchiveGradeYearLevelColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveProgramColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveSectionNameColumn { get; set; } = true;
        [ObservableProperty] public partial bool ShowArchiveEnrollmentStatusColumn { get; set; } = true;

        // Master & Archive Select-All Checkboxes
        [ObservableProperty] public partial bool IsAllMasterSelected { get; set; } = false;
        partial void OnIsAllMasterSelectedChanged(bool value)
        {
            foreach (var row in DisplayedStudents) row.IsSelected = value;
        }

        [ObservableProperty] public partial bool IsAllArchiveSelected { get; set; } = false;
        partial void OnIsAllArchiveSelectedChanged(bool value)
        {
            foreach (var row in DisplayedArchivedStudents) row.IsSelected = value;
        }

        // Confirmation Modal State (Shared by Archive, Restore, and Delete)
        [ObservableProperty] public partial bool IsConfirmModalOpen { get; set; } = false;
        [ObservableProperty] public partial ConfirmDeleteModalViewModel? ConfirmModal { get; set; }
        [ObservableProperty] public partial string ConfirmModalButtonText { get; set; } = "Confirm";
        [ObservableProperty] public partial string ConfirmModalButtonColor { get; set; } = "#F43F5E";

        // Toast Feedback State
        public Action<string>? ShowToastMessage { get; set; }
        [ObservableProperty] public partial string ToastMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsToastVisible { get; set; } = false;

        private async void TriggerToast(string message)
        {
            ToastMessage = message;
            IsToastVisible = true;
            ShowToastMessage?.Invoke(message);
            await Task.Delay(3000);
            if (ToastMessage == message) IsToastVisible = false;
        }

        [RelayCommand]
        public void PromptBulkArchive()
        {
            var selected = _allStudents.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                TriggerToast("Please select at least one student first.");
                return;
            }

            ConfirmModalButtonText = "Archive";
            ConfirmModalButtonColor = "#F59E0B";
            ConfirmModal = new ConfirmDeleteModalViewModel(
                "Archive Student(s)?",
                $"Are you sure you want to move {selected.Count} selected student(s) to the Archives?",
                async () =>
                {
                    var db = _dbService.GetConnection();
                    foreach (var row in selected)
                    {
                        row.DbModel.IsArchived = true;
                        row.IsSelected = false;
                    }
                    await db.UpdateAllAsync(selected.Select(r => r.DbModel), runInTransaction: false);

                    IsAllMasterSelected = false;
                    IsConfirmModalOpen = false;
                    ConfirmModal = null;

                    await LoadStudentsAsync();
                    OnStudentRosterChanged?.Invoke();
                    TriggerToast($"Archived {selected.Count} student(s).");
                },
                () => { IsConfirmModalOpen = false; ConfirmModal = null; }
            );
            IsConfirmModalOpen = true;
        }

        [RelayCommand]
        public void PromptBulkRestore()
        {
            var selected = _allArchivedStudents.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                TriggerToast("Please select at least one student first.");
                return;
            }

            ConfirmModalButtonText = "Restore";
            ConfirmModalButtonColor = "#10B981";
            ConfirmModal = new ConfirmDeleteModalViewModel(
                "Restore Student(s)?",
                $"Are you sure you want to restore {selected.Count} selected student(s) to the active Master Roster?",
                async () =>
                {
                    var db = _dbService.GetConnection();
                    foreach (var row in selected)
                    {
                        row.DbModel.IsArchived = false;
                        row.IsSelected = false;
                    }
                    await db.UpdateAllAsync(selected.Select(r => r.DbModel), runInTransaction: false);

                    IsAllArchiveSelected = false;
                    IsConfirmModalOpen = false;
                    ConfirmModal = null;

                    await LoadStudentsAsync();
                    OnStudentRosterChanged?.Invoke();
                    TriggerToast($"Restored {selected.Count} student(s).");
                },
                () => { IsConfirmModalOpen = false; ConfirmModal = null; }
            );
            IsConfirmModalOpen = true;
        }

        [RelayCommand]
        public void PromptBulkDelete()
        {
            var selected = _allArchivedStudents.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                TriggerToast("Please select at least one student first.");
                return;
            }

            ConfirmModalButtonText = "Delete Permanently";
            ConfirmModalButtonColor = "#F43F5E";
            ConfirmModal = new ConfirmDeleteModalViewModel(
                "Permanently Delete Student(s)?",
                $"Are you sure you want to permanently delete {selected.Count} selected student(s)?\n\nThis will permanently erase their profile, class enrollments, scores, group memberships, and attendance records.",
                async () =>
                {
                    var db = _dbService.GetConnection();
                    var targetIds = selected.Select(s => s.StudentID).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();

                    var rosters = (await db.Table<Centriku.Models.ClassRoster>().ToListAsync()).Where(r => targetIds.Contains(r.StudentID)).ToList();
                    var scores = (await db.Table<Centriku.Models.Score>().ToListAsync()).Where(s => s.StudentID != null && targetIds.Contains(s.StudentID)).ToList();
                    var attendance = (await db.Table<Centriku.Models.AttendanceRecord>().ToListAsync()).Where(a => a.StudentID != null && targetIds.Contains(a.StudentID)).ToList();
                    var groupMembers = (await db.Table<Centriku.Models.AssessmentGroupMember>().ToListAsync()).Where(m => targetIds.Contains(m.StudentID)).ToList();

                    foreach (var r in rosters) await db.DeleteAsync(r);
                    foreach (var s in scores) await db.DeleteAsync(s);
                    foreach (var a in attendance) await db.DeleteAsync(a);
                    foreach (var gm in groupMembers) await db.DeleteAsync(gm);
                    foreach (var row in selected) await db.DeleteAsync(row.DbModel);

                    IsAllArchiveSelected = false;
                    IsConfirmModalOpen = false;
                    ConfirmModal = null;

                    await LoadStudentsAsync();
                    OnStudentRosterChanged?.Invoke();
                    TriggerToast($"Permanently deleted {selected.Count} student(s).");
                },
                () => { IsConfirmModalOpen = false; ConfirmModal = null; }
            );
            IsConfirmModalOpen = true;
        }

        private void UpdateDisplayedArchivedStudents()
        {
            var lowerQuery = ArchiveSearchQuery?.ToLower() ?? string.Empty;

            var filtered = _allArchivedStudents.Where(s =>
                s.IsSelected ||
                string.IsNullOrWhiteSpace(lowerQuery) ||
                (s.StudentID?.ToLower().Contains(lowerQuery) == true) ||
                (s.LastName?.ToLower().Contains(lowerQuery) == true) ||
                (s.FirstName?.ToLower().Contains(lowerQuery) == true) ||
                (s.Program?.ToLower().Contains(lowerQuery) == true) ||
                (s.SectionName?.ToLower().Contains(lowerQuery) == true)
            ).ToList();

            DisplayedArchivedStudents = new ObservableCollection<StudentRowViewModel>(filtered);
            DynamicArchiveCounter = $"Showing: {filtered.Count} students";
        }
    }
}