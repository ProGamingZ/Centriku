using SQLite;
using System;
using System.IO;
using Centriku.Models;

namespace Centriku.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        public DatabaseService()
        {
            // Cross-platform safe path 
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Centriku.db3");
            
            _database = new SQLiteAsyncConnection(dbPath);

            // Create tables if they don't exist
            InitializeDatabaseAsync();
        }

        private async void InitializeDatabaseAsync()
        {
            await _database.CreateTableAsync<Student>();
            await _database.CreateTableAsync<TeacherClass>();
            await _database.CreateTableAsync<ClassRoster>();
            await _database.CreateTableAsync<Assessment>();
            await _database.CreateTableAsync<Score>();
            await _database.CreateTableAsync<ScoreHistory>();
        }

        // Expose the connection for your ViewModels to use
        public SQLiteAsyncConnection GetConnection() => _database;
    }
}