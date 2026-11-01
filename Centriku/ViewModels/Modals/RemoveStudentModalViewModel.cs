using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Centriku.Models;
using Centriku.Services;

namespace Centriku.ViewModels.Modals
{
    public partial class RemoveStudentModalViewModel : ObservableObject
    {
        private readonly IDatabaseService _dbService;
        private readonly int _classId;
        private readonly List<Student> _studentsToRemove;
        private readonly Func<Task> _onSuccess;
        private readonly Action _onClose;
        private readonly Action<string> _showToast;

        [ObservableProperty] public partial bool IsProcessing { get; set; } = false;
        [ObservableProperty] public partial string ModalMessage { get; set; } = string.Empty;

        public RemoveStudentModalViewModel(IDatabaseService dbService, int classId, List<Student> students, Func<Task> onSuccess, Action onClose, Action<string> showToast)
        {
            _dbService = dbService;
            _classId = classId;
            _studentsToRemove = students;
            _onSuccess = onSuccess;
            _onClose = onClose;
            _showToast = showToast;
            
            ModalMessage = $"Are you sure you want to unenroll {_studentsToRemove.Count} selected student(s) from this class?\n\nThey will be removed from the class roster immediately.";
        }

        [RelayCommand]
        private async Task ConfirmRemoveAsync()
        {
            IsProcessing = true;
            try 
            {
                var db = _dbService.GetConnection();
                var classAssessments = await db.Table<Assessment>().Where(a => a.ClassID == _classId).ToListAsync();
                var assessmentIds = classAssessments.Select(a => a.AssessmentID).ToHashSet();
                var studentIds = _studentsToRemove.Select(s => s.StudentID).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();

                var rostersToDelete = (await db.Table<ClassRoster>().Where(r => r.ClassID == _classId).ToListAsync())
                    .Where(r => studentIds.Contains(r.StudentID)).ToList();

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
                foreach (var r in rostersToDelete) await db.DeleteAsync(r);
                
                int count = _studentsToRemove.Count;
                _showToast?.Invoke(count == 1 
                    ? $"Successfully unenrolled {_studentsToRemove[0].FirstName} {_studentsToRemove[0].LastName}." 
                    : $"Successfully unenrolled {count} students.");
                    
                await _onSuccess(); 
                _onClose(); 
            }
            catch (Exception ex)
            {
                _showToast?.Invoke($"Error unenrolling students: {ex.Message}");
            }
            finally { IsProcessing = false; }
        }

        [RelayCommand]
        private void Cancel() => _onClose();
    }
}