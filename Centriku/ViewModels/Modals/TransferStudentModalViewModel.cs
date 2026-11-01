using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels.Modals
{
    public partial class TransferStudentModalViewModel : ObservableObject
    {
        private readonly IDatabaseService _dbService;
        private readonly int _classId;
        private readonly List<Student> _studentsToTransfer;
        private readonly Func<Task> _onSuccess;
        private readonly Action _onClose;
        private readonly Action<string> _showToast;

        [ObservableProperty] public partial bool IsProcessing { get; set; } = false;
        [ObservableProperty] public partial string ModalMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial ObservableCollection<TeacherClass> AvailableTransferClasses { get; set; } = new();
        [ObservableProperty] public partial TeacherClass? SelectedTransferClass { get; set; }

        public TransferStudentModalViewModel(IDatabaseService dbService, int classId, string classTitle, List<Student> students, Func<Task> onSuccess, Action onClose, Action<string> showToast)
        {
            _dbService = dbService;
            _classId = classId;
            _studentsToTransfer = students;
            _onSuccess = onSuccess;
            _onClose = onClose;
            _showToast = showToast;
            
            ModalMessage = $"Move {_studentsToTransfer.Count} selected student(s) from {classTitle} to another class?";
        }
        
        public async Task LoadClassesAsync()
        {
            var db = _dbService.GetConnection();
            var allClasses = await db.Table<TeacherClass>().ToListAsync();
            AvailableTransferClasses = new ObservableCollection<TeacherClass>(allClasses.Where(c => c.ClassID != _classId && c.IsActive));
            SelectedTransferClass = AvailableTransferClasses.FirstOrDefault();
        }

        [RelayCommand]
        private async Task ConfirmTransferAsync()
        {
            IsProcessing = true;
            try 
            {
                if (SelectedTransferClass == null) return;
                var db = _dbService.GetConnection();

                int targetClassId = SelectedTransferClass.ClassID;
                await db.CreateTableAsync<Score>();
                await db.CreateTableAsync<AttendanceRecord>();
                await db.CreateTableAsync<AssessmentGroupMember>();
                var classAssessments = await db.Table<Assessment>().Where(a => a.ClassID == _classId).ToListAsync();
                var assessmentIds = classAssessments.Select(a => a.AssessmentID).ToHashSet();
                var studentIds = _studentsToTransfer.Select(s => s.StudentID).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();

                var rostersToUpdate = (await db.Table<ClassRoster>().Where(r => r.ClassID == _classId).ToListAsync())
                    .Where(r => studentIds.Contains(r.StudentID)).ToList();

                // Check if any student is already enrolled in the destination class to avoid duplicate roster rows
                var existingTargetIds = (await db.Table<ClassRoster>().Where(r => r.ClassID == targetClassId).ToListAsync())
                    .Select(r => r.StudentID).ToHashSet();

                var attendanceToDelete = (await db.Table<AttendanceRecord>().Where(a => a.ClassID == _classId).ToListAsync())
                    .Where(a => a.StudentID != null && studentIds.Contains(a.StudentID)).ToList();

                var scoresToDelete = new List<Score>();
                var groupMembersToDelete = new List<AssessmentGroupMember>();

                if (assessmentIds.Count > 0)
                {
                    var allScores = await db.Table<Score>().ToListAsync();
                    scoresToDelete = allScores.Where(s => assessmentIds.Contains(s.AssessmentID) && s.StudentID != null && studentIds.Contains(s.StudentID)).ToList();

                    var allMembers = await db.Table<AssessmentGroupMember>().ToListAsync();
                    groupMembersToDelete = allMembers.Where(m => assessmentIds.Contains(m.AssessmentID) && studentIds.Contains(m.StudentID)).ToList();
                }

                foreach (var s in scoresToDelete) await db.DeleteAsync(s);
                foreach (var m in groupMembersToDelete) await db.DeleteAsync(m);
                foreach (var a in attendanceToDelete) await db.DeleteAsync(a);

                foreach (var r in rostersToUpdate) 
                {
                    if (existingTargetIds.Contains(r.StudentID))
                    {
                        await db.DeleteAsync(r);
                    }
                    else
                    {
                        r.ClassID = targetClassId;
                        r.HasRecited = false;
                        await db.UpdateAsync(r);
                    }
                }

                int count = _studentsToTransfer.Count;
                _showToast?.Invoke(count == 1 
                    ? $"Successfully transferred {_studentsToTransfer[0].FirstName} {_studentsToTransfer[0].LastName}." 
                    : $"Successfully transferred {count} students.");
                    
                await _onSuccess(); 
                _onClose();
            }
            catch (Exception ex)
            {
                _showToast?.Invoke($"Error transferring students: {ex.Message}");
            }
            finally { IsProcessing = false; }
        }

        [RelayCommand]
        private void Cancel() => _onClose();
    }
}