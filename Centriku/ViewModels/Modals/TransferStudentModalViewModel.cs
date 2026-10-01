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
        private readonly int _classId;
        private readonly List<Student> _studentsToTransfer;
        private readonly Func<Task> _onSuccess;
        private readonly Action _onClose;
        private readonly Action<string> _showToast;

        [ObservableProperty] public partial bool IsProcessing { get; set; } = false;
        [ObservableProperty] public partial string ModalMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial ObservableCollection<TeacherClass> AvailableTransferClasses { get; set; } = new();
        [ObservableProperty] public partial TeacherClass? SelectedTransferClass { get; set; }

        public TransferStudentModalViewModel(int classId, string classTitle, List<Student> students, Func<Task> onSuccess, Action onClose, Action<string> showToast)
        {
            _classId = classId;
            _studentsToTransfer = students;
            _onSuccess = onSuccess;
            _onClose = onClose;
            _showToast = showToast;
            
            ModalMessage = $"Move {_studentsToTransfer.Count} selected student(s) from {classTitle} to another class?";
        }
        
        public async Task LoadClassesAsync()
        {
            var db = new DatabaseService().GetConnection();
            var allClasses = await db.Table<TeacherClass>().ToListAsync();
            AvailableTransferClasses = new ObservableCollection<TeacherClass>(allClasses.Where(c => c.ClassID != _classId));
            SelectedTransferClass = AvailableTransferClasses.FirstOrDefault();
        }

        [RelayCommand]
        private async Task ConfirmTransferAsync()
        {
            IsProcessing = true;
            try 
            {
                if (SelectedTransferClass == null) return;
                var db = new DatabaseService().GetConnection();
                var classAssessments = await db.Table<Assessment>().Where(a => a.ClassID == _classId).ToListAsync();
                var assessmentIds = classAssessments.Select(a => a.AssessmentID).ToList();
                var studentIds = _studentsToTransfer.Select(s => s.StudentID).ToList();

                var rostersToUpdate = await db.Table<ClassRoster>()
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
                    foreach (var r in rostersToUpdate) 
                    {
                        r.ClassID = SelectedTransferClass.ClassID;
                        tran.Update(r);
                    }
                    foreach (var m in groupMembersToDelete) tran.Delete(m);
                });

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