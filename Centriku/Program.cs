using System;
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
            try
            {
                // 1. Catch exceptions that happen on background threads
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    string errorMsg = $"[FATAL BACKGROUND CRASH]: {e.ExceptionObject}";
                    Console.WriteLine(errorMsg);
                    System.IO.File.WriteAllText("crash_log.txt", errorMsg);
                };

                // This is your standard Avalonia startup
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                // 2. Catch exceptions that happen during app startup or on the main UI thread
                string errorMsg = $"[CRITICAL STARTUP ERROR]:\nMessage: {ex.Message}\nStack Trace:\n{ex.StackTrace}";
                Console.WriteLine(errorMsg);
                System.IO.File.WriteAllText("crash_log.txt", ex.ToString());
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