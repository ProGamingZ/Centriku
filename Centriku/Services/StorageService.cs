using System;
using System.IO;
using System.Runtime.InteropServices; // Required for OS checking

namespace Centriku.Services
{
    public static class StorageService
    {
        // Gets the main "Centriku_Data" folder path dynamically based on the OS.
        public static string GetAppFolderPath()
        {
            string basePath;

            // 1. Check which Operating System the app is currently running on
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // WINDOWS: Put the data folder right next to the .exe file (Portable mode)
                basePath = AppDomain.CurrentDomain.BaseDirectory;
            }
            else
            {
                // MAC/LINUX: Safely store it in the user's profile to prevent deletion during app updates
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            
            // 2. Append our custom folder name
            string appFolder = Path.Combine(basePath, "Centriku_Data");

            // 3. If it doesn't exist yet, create it instantly
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            return appFolder;
        }

        // Gets the exact file path for the SQLite database.
        public static string GetDatabasePath()
        {
            return Path.Combine(GetAppFolderPath(), "centriku.db");
        }

        // Gets the folder path for exported CSV/Excel files
        public static string GetExportsFolderPath()
        {
            string exportFolder = Path.Combine(GetAppFolderPath(), "Exports");
            
            if (!Directory.Exists(exportFolder))
            {
                Directory.CreateDirectory(exportFolder);
            }
            
            return exportFolder;
        }

        // Gets the folder path for the Excel Templates
        public static string GetTemplateFolderPath()
        {
            string templateFolder = Path.Combine(GetAppFolderPath(), "template");
            
            if (!Directory.Exists(templateFolder))
            {
                Directory.CreateDirectory(templateFolder);
            }
            
            return templateFolder;
        }

        // Copies the bundled template into the user's data folder on first run
        public static void InitializeDefaultTemplates()
        {
            string targetFolder = GetTemplateFolderPath();
            string targetFile = Path.Combine(targetFolder, "NwSSU-Class-Record.xlsx");

            // The file bundled by the compiler (preserves folder structure)
            string sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "template", "NwSSU-Class-Record.xlsx");

            // If the bundled file exists but the user doesn't have it yet, copy it over!
            if (File.Exists(sourceFile) && !File.Exists(targetFile))
            {
                File.Copy(sourceFile, targetFile);
            }
        }
    }
}