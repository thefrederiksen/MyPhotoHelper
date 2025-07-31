using System;
using System.IO;
using System.Text;

namespace MyPhotoHelper.Services
{
    /// <summary>
    /// Early error logger that works before any dependency injection or configuration
    /// </summary>
    public static class StartupErrorLogger
    {
        private static readonly object _lockObject = new object();
        private static string? _logPath;

        static StartupErrorLogger()
        {
            try
            {
                // Try to create log directory in AppData
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var logDir = Path.Combine(appData, "MyPhotoHelper", "Logs");
                Directory.CreateDirectory(logDir);
                _logPath = Path.Combine(logDir, $"startup_error_{DateTime.Now:yyyyMMdd}.log");
            }
            catch
            {
                // Fallback to temp directory
                try
                {
                    var tempPath = Path.GetTempPath();
                    _logPath = Path.Combine(tempPath, $"MyPhotoHelper_startup_error_{DateTime.Now:yyyyMMdd}.log");
                }
                catch
                {
                    // If even temp fails, we can't log to file
                    _logPath = null;
                }
            }
        }

        public static void LogError(string message, Exception? exception = null)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = new StringBuilder();
            logEntry.AppendLine($"[{timestamp}] ERROR: {message}");
            
            if (exception != null)
            {
                logEntry.AppendLine($"Exception Type: {exception.GetType().FullName}");
                logEntry.AppendLine($"Message: {exception.Message}");
                logEntry.AppendLine($"Stack Trace:\n{exception.StackTrace}");
                
                var inner = exception.InnerException;
                while (inner != null)
                {
                    logEntry.AppendLine($"\nInner Exception: {inner.GetType().FullName}");
                    logEntry.AppendLine($"Message: {inner.Message}");
                    logEntry.AppendLine($"Stack Trace:\n{inner.StackTrace}");
                    inner = inner.InnerException;
                }
            }
            
            logEntry.AppendLine(new string('-', 80));

            // Try to write to file
            if (!string.IsNullOrEmpty(_logPath))
            {
                try
                {
                    lock (_lockObject)
                    {
                        File.AppendAllText(_logPath, logEntry.ToString());
                    }
                }
                catch
                {
                    // Can't write to file, but don't throw
                }
            }

            // Also write to console/debug output
            Console.Error.WriteLine(logEntry.ToString());
            System.Diagnostics.Debug.WriteLine(logEntry.ToString());
        }

        public static string GetLogPath()
        {
            return _logPath ?? "No log file available";
        }

        public static string GetLastErrors(int maxLines = 100)
        {
            if (string.IsNullOrEmpty(_logPath) || !File.Exists(_logPath))
            {
                return "No error log file found.";
            }

            try
            {
                var lines = File.ReadAllLines(_logPath);
                var startIndex = Math.Max(0, lines.Length - maxLines);
                var relevantLines = new string[lines.Length - startIndex];
                Array.Copy(lines, startIndex, relevantLines, 0, relevantLines.Length);
                return string.Join(Environment.NewLine, relevantLines);
            }
            catch (Exception ex)
            {
                return $"Error reading log file: {ex.Message}";
            }
        }
    }
}