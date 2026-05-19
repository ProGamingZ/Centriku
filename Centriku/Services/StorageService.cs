using System;
using System.IO;

namespace Centriku.Services
{
    public static class StorageService
    {
        /// Gets the main "Centriku_Data" folder path and guarantees it exists.
        public static string GetAppFolderPath()
        {
            // 1. Find the User's Profile (C:\Users\Username)
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            // 2. Append our custom folder name
            string appFolder = Path.Combine(userProfile, "Centriku_Data");

            // 3. If it doesn't exist yet, create it instantly!
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            return appFolder;
        }

        /// Gets the exact file path for the SQLite database.
        public static string GetDatabasePath()
        {
            return Path.Combine(GetAppFolderPath(), "centriku.db");
        }

        /// Gets the folder path for exported CSV/Excel files (Useful for Phase A!)
        public static string GetExportsFolderPath()
        {
            string exportFolder = Path.Combine(GetAppFolderPath(), "Exports");
            
            if (!Directory.Exists(exportFolder))
            {
                Directory.CreateDirectory(exportFolder);
            }
            
            return exportFolder;
        }
    }
}