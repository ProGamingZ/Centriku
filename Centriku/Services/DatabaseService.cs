using System;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using Centriku.Models;

namespace Centriku.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;

        public DatabaseService()
        {
            string dbPath = StorageService.GetDatabasePath();
            _database = new SQLiteAsyncConnection(dbPath);
        }

        // 3. This method builds all 8 of your tables
        public async Task InitializeDatabaseAsync()
        {
            await _database.CreateTableAsync<Student>();
            await _database.CreateTableAsync<TeacherClass>();
            await _database.CreateTableAsync<ClassRoster>();
            await _database.CreateTableAsync<Assessment>();
            await _database.CreateTableAsync<Score>();
            await _database.CreateTableAsync<ScoreHistory>();
            await _database.CreateTableAsync<GradingTemplate>();
            await _database.CreateTableAsync<GradingCategory>();
            await _database.CreateTableAsync<AttendanceRecord>();
            await _database.CreateTableAsync<MasterQuarterlyGrade>();
        }
        public SQLiteAsyncConnection GetConnection() => _database;
    }
}