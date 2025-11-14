using System;
using System.IO;

namespace GroundUp.infrastructure.utilities
{
    public static class FileLogger
    {
        private static readonly string LogFilePath = @"c:\temp\roblog.txt";
        private static readonly object _lock = new object();

        static FileLogger()
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Clear the log file on startup
            try
            {
                File.WriteAllText(LogFilePath, $"=== Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ==={Environment.NewLine}");
            }
            catch
            {
                // If we can't write to the file, just continue
            }
        }

        public static void Log(string message)
        {
            try
            {
                lock (_lock)
                {
                    var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, logMessage);
                }
            }
            catch
            {
                // Silently fail if we can't write to the log
            }
        }

        public static void LogObject(string label, object obj)
        {
            if (obj == null)
            {
                Log($"{label}: NULL");
                return;
            }

            Log($"{label}: {obj}");
        }
    }
}
