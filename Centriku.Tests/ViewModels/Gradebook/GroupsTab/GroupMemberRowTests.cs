using Xunit;
using Moq;
using System.Threading.Tasks;
using Centriku.Models;
using Centriku.ViewModels;
using Centriku.Services;
using Centriku.Tests.Factories;

namespace Centriku.Tests.ViewModels
{
    public class GroupMemberRowTests
    {
        [Fact]
        public void CalculateTotal_ShouldAddGroupAndIndividualScores_Correctly()
        {
            // 1. ARRANGE (Set up the fake environment)
            var mockDb = new Mock<IDatabaseService>(); // Creates a ghost Database Service!

            var fakeAssessment = new Assessment { MaxScore = 50.0 };
            var fakeStudent = new Student { FirstName = "John", LastName = "Doe" };
            var fakeMember = new AssessmentGroupMember { IndividualScore = 15.0 };
            
            var fakeGroup = new AssessmentGroup { GroupScore = 30.0 };
            var fakeCard = new GroupCardViewModel(fakeGroup, fakeAssessment, async (card) => await Task.CompletedTask);

            // Instantiate the ViewModel exactly as your app would, but feed it the fakes
            var rowViewModel = new GroupMemberRowViewModel(
                mockDb.Object, 
                fakeMember, 
                fakeStudent, 
                fakeAssessment, 
                fakeCard, 
                () => { } // Empty action for the save callback
            );

            // 2. ACT (Trigger the math engine)
            // Group (30) + Individual (15) = 45
            rowViewModel.CalculateTotal(fakeCard.GroupScore);

            // 3. ASSERT (Verify the results)
            Assert.Equal(45.0, rowViewModel.TotalComputedGrade);
        }

        [Fact]
        public void CalculateTotal_ShouldNotExceedMaxScore_WhenCombinedScoresAreTooHigh()
        {
            // 1. ARRANGE (Using the Factory!)
            var mockDb = new Mock<IDatabaseService>();
            
            // Instantly summon perfect fake data without cluttering the test
            var fakeAssessment = TestDataFactory.CreateFakeAssessment(maxScore: 50.0);
            var fakeStudent = TestDataFactory.CreateFakeStudent();
            
            var fakeMember = new AssessmentGroupMember { IndividualScore = 20.0 };
            var fakeGroup = new AssessmentGroup { GroupScore = 40.0 };
            var fakeCard = new GroupCardViewModel(fakeGroup, fakeAssessment, async (card) => await Task.CompletedTask);

            var rowViewModel = new GroupMemberRowViewModel(mockDb.Object, fakeMember, fakeStudent, fakeAssessment, fakeCard, () => { });

            // 2. ACT
            rowViewModel.CalculateTotal(fakeCard.GroupScore);

            // 3. ASSERT (40 + 20 = 60, but it MUST clamp down to 50.0)
            Assert.Equal(50.0, rowViewModel.TotalComputedGrade);
        }
    }
}