using SQLite;

namespace Centriku.Services
{
    public interface IDatabaseService
    {
        SQLiteAsyncConnection GetConnection();
    }
}