using Xunit;
using SQLite;
using System;
using System.IO;
using System.Threading.Tasks;
using Centriku.Models;

namespace Centriku.Tests.Services
{
    public class DatabaseCrudTests
    {
        [Fact]
        public async Task Database_ShouldCorrectlyInsertAndDelete_AssessmentGroups()
        {
            // 1. ARRANGE: Create an isolated temporary database
            string dbPath = Path.Combine(Path.GetTempPath(), $"CrudTest_{Guid.NewGuid()}.db");
            var db = new SQLiteAsyncConnection(dbPath);
            
            await db.CreateTableAsync<AssessmentGroup>();

            var testGroup = new AssessmentGroup 
            { 
                AssessmentID = 99, 
                ClassID = 101, 
                GroupName = "Integration Test Group", 
                GroupScore = 85.5 
            };

            // 2. ACT & ASSERT: CREATE / READ
            await db.InsertAsync(testGroup);
            var savedGroup = await db.Table<AssessmentGroup>().FirstOrDefaultAsync();
            
            Assert.NotNull(savedGroup);
            Assert.Equal("Integration Test Group", savedGroup.GroupName); 
            Assert.True(savedGroup.GroupID > 0); 

            // 3. ACT & ASSERT: DELETE
            await db.DeleteAsync(savedGroup);
            var remainingGroups = await db.Table<AssessmentGroup>().ToListAsync();
            
            Assert.Empty(remainingGroups); 
        }
    }
}