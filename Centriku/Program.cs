using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;

namespace Centriku
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            string logPath = "crash_log.txt";

            // 1. Catch exceptions that happen on background threads
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                string errorMsg = $"\n[{DateTime.Now}] [FATAL BACKGROUND CRASH]:\n{e.ExceptionObject}\n";
                Console.WriteLine(errorMsg);
                File.AppendAllText(logPath, errorMsg);
            };

            // 2. Catch async task exceptions (e.g., database queries that fail silently)
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                string errorMsg = $"\n[{DateTime.Now}] [UNOBSERVED ASYNC TASK CRASH]:\n{e.Exception}\n";
                Console.WriteLine(errorMsg);
                File.AppendAllText(logPath, errorMsg);
                // Optional: e.SetObserved(); keeps the app alive, but letting it crash is better for debugging
            };

            try
            {
                // This is your standard Avalonia startup
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                // 3. Catch exceptions that happen during app startup or on the main UI thread
                string errorMsg = $"\n[{DateTime.Now}] [CRITICAL UI/STARTUP ERROR]:\n{ex}\n";
                Console.WriteLine(errorMsg);
                File.AppendAllText(logPath, errorMsg);
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}