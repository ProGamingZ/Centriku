using CommunityToolkit.Mvvm.ComponentModel;
using Centriku.Models;

namespace Centriku.ViewModels
{
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
}