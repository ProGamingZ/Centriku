using Xunit;
using SQLite;
using System.Threading.Tasks;
using Centriku.Models;

namespace Centriku.Tests.Services
{
    public class DatabaseCrudTests
    {
        [Fact]
        public async Task Database_ShouldCorrectlyInsertAndDelete_AssessmentGroups()
        {
            // 1. ARRANGE: Create an IN-MEMORY SQLite database 
            // Passing ":memory:" tells SQLite not to create a physical file.
            var db = new SQLiteAsyncConnection(":memory:");
            
            // Generate the table structure in RAM
            await db.CreateTableAsync<AssessmentGroup>();

            var testGroup = new AssessmentGroup 
            { 
                AssessmentID = 99, 
                ClassID = 101, 
                GroupName = "Integration Test Group", 
                GroupScore = 85.5 
            };

            // ==========================================
            // 2. ACT & ASSERT: CREATE / READ
            // ==========================================
            await db.InsertAsync(testGroup);
            
            var savedGroup = await db.Table<AssessmentGroup>().FirstOrDefaultAsync();
            
            Assert.NotNull(savedGroup); // Proves the database saved something
            Assert.Equal("Integration Test Group", savedGroup.GroupName); // Proves data wasn't corrupted
            Assert.True(savedGroup.GroupID > 0); // Proves Auto-Increment Primary Keys work

            // ==========================================
            // 3. ACT & ASSERT: DELETE
            // ==========================================
            await db.DeleteAsync(savedGroup);

            var remainingGroups = await db.Table<AssessmentGroup>().ToListAsync();
            
            Assert.Empty(remainingGroups); // Proves the database successfully wiped the record
        }
    }
}