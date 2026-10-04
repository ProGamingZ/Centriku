using Xunit;
using System.Threading.Tasks;
using Centriku.Models;
using Centriku.ViewModels;
using Centriku.Tests.Factories;

namespace Centriku.Tests.ViewModels
{
    public class PoliciesViewModelTests
    {
        private async Task PreparePoliciesTablesAsync(FakeDatabaseService fakeDb)
        {
            var db = fakeDb.GetConnection();
            await db.CreateTablesAsync(SQLite.CreateFlags.None,
                typeof(GradingTemplate), typeof(GradingCategory), typeof(TeacherClass));
        }

        [Fact]
        public async Task SavePolicy_ShouldInsertTemplate_AndMapCategories()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PreparePoliciesTablesAsync(fakeDb);
            
            var vm = new PoliciesViewModel(fakeDb);
            vm.TemplateName = "Standard Policy";
            vm.PassingGrade = 75m;
            vm.NrfgBaseValue = 50.0;
            
            // Clear default categories and set up a valid 100% policy
            vm.Categories.Clear();
            vm.AddCategoryCommand.Execute(null); 
            vm.Categories[0].Name = "Exam";
            vm.Categories[0].Weight = 100m;
            
            // 2. ACT
            await vm.SavePolicyCommand.ExecuteAsync(null);

            // 3. ASSERT
            var db = fakeDb.GetConnection();
            var templates = await db.Table<GradingTemplate>().ToListAsync();
            var categories = await db.Table<GradingCategory>().ToListAsync();

            // Prove the template saved correctly
            Assert.Single(templates);
            Assert.Equal("Standard Policy", templates[0].TemplateName);

            // Prove the category saved and was mapped to the correct template ID
            Assert.Single(categories);
            Assert.Equal("Exam", categories[0].Name);
            Assert.Equal(100.0, categories[0].Weight);
            Assert.Equal(templates[0].TemplateID, categories[0].TemplateID);
        }

        [Fact]
        public async Task ConfirmDelete_ShouldWipeTemplate_AndCascadingCategories()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await PreparePoliciesTablesAsync(fakeDb);
            var db = fakeDb.GetConnection();

            // Setup a fake template and child category in the DB
            var template = new GradingTemplate { TemplateName = "Old Policy" };
            await db.InsertAsync(template);
            await db.InsertAsync(new GradingCategory { TemplateID = template.TemplateID, Name = "Quiz", Weight = 100 });

            var vm = new PoliciesViewModel(fakeDb);
            
            // Initiate the delete check (verifies no active classes are using it)
            vm.InitiateDeleteCommand.Execute(template); 
            
            // SYNCHRONIZATION: InitiateDelete is 'async void', so we give SQLite 50ms to check the TeacherClass table!
            await Task.Delay(50);

            // Prove the UI allowed the delete prompt
            Assert.True(vm.CanConfirmDelete);

            // 2. ACT
            await vm.ConfirmDeleteCommand.ExecuteAsync(null);

            // 3. ASSERT
            var templates = await db.Table<GradingTemplate>().ToListAsync();
            var categories = await db.Table<GradingCategory>().ToListAsync();

            // Prove the template is gone
            Assert.Empty(templates);
            
            // Prove the SQL Transaction safely wiped the orphaned categories
            Assert.Empty(categories); 
        }
    }
}