using Xunit;
using System.Threading.Tasks;
using Centriku.ViewModels.Settings;
using Centriku.Tests.Factories;

namespace Centriku.Tests.ViewModels.Settings
{
    public class SettingsViewModelsTests
    {
        [Fact]
        public async Task ExportSettings_ResetDefaults_ShouldRestoreBaseValues()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await fakeDb.PrepareSettingsTablesAsync();
            
            var vm = new ExportSettingsViewModel(fakeDb);
            
            // Mess up the settings
            vm.FileNamingFormat = "BROKEN_FORMAT";
            vm.ExportIncludeArchived = true;

            // 2. ACT
            await vm.ResetDefaultsCommand.ExecuteAsync(null);

            // 3. ASSERT
            Assert.Equal("[Class]_[Section]_[Type]_[Date]", vm.FileNamingFormat);
            Assert.False(vm.ExportIncludeArchived);
        }

        [Fact]
        public async Task ImportSettings_ChangingDuplicateRule_ShouldUpdateBackendModel()
        {
            // 1. ARRANGE
            var vm = new ImportSettingsViewModel(); // Currently doesn't require IDatabaseService injection

            // 2. ACT
            vm.SelectedDuplicateRule = "Skip Duplicate (Do not import)";

            // 3. ASSERT (Verifies the UI dropdown correctly maps to the backend string)
            Assert.Equal("Skip", vm.CurrentSettings.DuplicateHandlingRule);
        }

        [Fact]
        public async Task SystemSettings_RunIntegrityCheck_ShouldUpdateUIStatusToHealthy()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await fakeDb.PrepareSettingsTablesAsync(); // Uses Factory helper!
            var vm = new SystemSettingsViewModel(fakeDb);
            
            // Start with a blank slate
            vm.DbStatus = "Checking...";

            // 2. ACT
            await vm.RunIntegrityCheckCommand.ExecuteAsync(null);

            // 3. ASSERT
            Assert.Equal("Healthy", vm.DbStatus);
            Assert.Contains("Result: ok", vm.DiagnosticOutput);
        }
        [Fact]
        public async Task SystemSettings_OptimizeSystem_ShouldUpdateUIStatusAndFeedback()
        {
            // 1. ARRANGE
            var fakeDb = new FakeDatabaseService();
            await fakeDb.PrepareSettingsTablesAsync();
            var db = fakeDb.GetConnection();

            // Seed 1 orphaned score
            await db.InsertAsync(new Centriku.Models.Score { AssessmentID = 999, StudentID = "GHOST-01", PointsEarned = 50 });

            var vm = new SystemSettingsViewModel(fakeDb);

            // 2. ACT
            await vm.OptimizeSystemCommand.ExecuteAsync(null);

            // 3. ASSERT
            Assert.Equal("Healthy", vm.DbStatus);
            Assert.Contains("Cleaned 1 unused ghost record(s)", vm.StandardFeedbackMessage);
        }
    }
}