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
        private readonly int _classId;
        private readonly List<Student> _studentsToRemove;
        private readonly Func<Task> _onSuccess;
        private readonly Action _onClose;
        private readonly Action<string> _showToast;

        [ObservableProperty] public partial bool IsProcessing { get; set; } = false;
        [ObservableProperty] public partial string ModalMessage { get; set; } = string.Empty;

        public RemoveStudentModalViewModel(int classId, List<Student> students, Func<Task> onSuccess, Action onClose, Action<string> showToast)
        {
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
                var db = new DatabaseService().GetConnection();
                var classAssessments = await db.Table<Assessment>().Where(a => a.ClassID == _classId).ToListAsync();
                var assessmentIds = classAssessments.Select(a => a.AssessmentID).ToList();
                var studentIds = _studentsToRemove.Select(s => s.StudentID).ToList();

                var rostersToDelete = await db.Table<ClassRoster>()
                    .Where(r => r.ClassID == _classId && studentIds.Contains(r.StudentID))
                    .ToListAsync();

                var groupMembersToDelete = new List<AssessmentGroupMember>();
                if (assessmentIds.Any())
                {
                    groupMembersToDelete = await db.Table<AssessmentGroupMember>()
                        .Where(m => studentIds.Contains(m.StudentID) && assessmentIds.Contains(m.AssessmentID))
                        .ToListAsync();
                }

                await db.RunInTransactionAsync(tran => 
                {
                    foreach (var r in rostersToDelete) tran.Delete(r);
                    foreach (var m in groupMembersToDelete) tran.Delete(m);
                });
                
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