using Xunit;
using Moq;
using Centriku.Models;
using Centriku.ViewModels;
using Centriku.Services;

namespace Centriku.Tests.ViewModels.Gradebook
{
    public class DataGridTests
    {
        [Fact]
        public void AttendanceGridRow_ShouldCalculate_TotalsCorrectly()
        {
            // 1. ARRANGE
            var mockDb = new Mock<IDatabaseService>(); // Dummy service, no DB needed
            var student = new Student { FirstName = "John", LastName = "Doe" };
            var row = new AttendanceGridRowViewModel(student);

            // Add some mock attendance cells. (SessionHours defaults to 1.5 hrs per record)
            row.Cells["Day1"] = new AttendanceCellViewModel(mockDb.Object, new AttendanceRecord { Status = "P", SessionHours = 1.5 }, () => {}, null!);
            row.Cells["Day2"] = new AttendanceCellViewModel(mockDb.Object, new AttendanceRecord { Status = "P", SessionHours = 1.5 }, () => {}, null!);
            row.Cells["Day3"] = new AttendanceCellViewModel(mockDb.Object, new AttendanceRecord { Status = "A", SessionHours = 1.5 }, () => {}, null!);
            row.Cells["Day4"] = new AttendanceCellViewModel(mockDb.Object, new AttendanceRecord { Status = "L", SessionHours = 1.5 }, () => {}, null!);

            // 2. ACT
            // Changed from int to double to support fractional contact hours
            double presents = row.TotalP;
            double absences = row.TotalA;
            double lates = row.TotalL;

            // 3. ASSERT
            Assert.Equal(3.0, presents); // 2 days * 1.5 hrs
            Assert.Equal(1.5, absences); // 1 day * 1.5 hrs
            Assert.Equal(1.5, lates);    // 1 day * 1.5 hrs
        }

        [Fact]
        public void GroupCard_ShouldCalculate_MemberTotalsCorrectly()
        {
            // 1. ARRANGE
            var mockDb = new Mock<IDatabaseService>(); 
            var assessment = new Assessment { MaxScore = 100 };
            var group = new AssessmentGroup { GroupScore = 80 }; // Group gets 80/100
            
            var groupCard = new GroupCardViewModel(group, assessment, _ => System.Threading.Tasks.Task.CompletedTask);

            var studentInfo = new Student { FirstName = "Jane", LastName = "Smith" };
            
            // Student gets +5 individual bonus points
            var member = new AssessmentGroupMember { IndividualScore = 5 }; 
            
            var memberRow = new GroupMemberRowViewModel(mockDb.Object, member, studentInfo, assessment, groupCard, () => {});
            groupCard.Members.Add(memberRow);

            // 2. ACT
            groupCard.UpdateAllMemberTotals();

            // 3. ASSERT
            // Group Score (80) + Individual Score (5) = 85 Total
            Assert.Equal(85, memberRow.TotalComputedGrade);
        }
    }
}