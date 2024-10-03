using ElmahCore;
using ReserveBlockCore.Utilities;
using System.Runtime.CompilerServices;

namespace ReserveBlockCore
{
    public static class VFXLogging
    {
        private static ErrorLog _errorLog;

        // This method needs to be called to initialize the ErrorLog instance
        public static void Initialize(ErrorLog errorLog)
        {
            _errorLog = errorLog;
        }
        public static async Task ClearElmah()
        {
            //Keep ALL files
            if (Globals.ElmahFileStore == 0)
                return;

            string dbPath = GetPathUtility.GetDatabasePath();
            string logPath = Path.Combine(dbPath, "elmah.xml");
            if (Directory.Exists(logPath))
            {
                var logFiles = Directory.GetFiles(logPath, "*.xml"); // Adjust the file extension if needed
                int fileCount = logFiles.Length;
                if (fileCount > Globals.ElmahFileStore)
                    Directory.Delete(logPath, true);
            }
        }
        public static void LogEvent()
        {
            if (_errorLog == null)
            {
                throw new InvalidOperationException("Logger not initialized.");
            }

            var error = new Error(new Exception("Test error"));
            _errorLog.Log(error);
        }
        public static void LogInfo(string message, string loc, bool isSC = false, bool isVal = false)
        {
            if (_errorLog == null)
            {
                throw new InvalidOperationException("Logger not initialized.");
            }

            var error = new Error(new Exception("Info Logged"))
            {
                Source = $"{loc}",
                StatusCode = 200,
                Time = DateTime.Now,
                Type = !isSC ? !isVal ? "Info Logged" : "Validator Info Logged" : "SC Info Logged",
                ApplicationName = "VFX Core CLI",
                Message = loc
            };
            error.MessageLog.Add(new ElmahLogMessageEntry { Level = LogLevel.None, Message = message, Collapsed = true, TimeStamp = DateTime.Now });
            _errorLog.Log(error);
        }

        public static void LogError(string message, string loc, bool isSC = false)
        {
            if (_errorLog == null)
            {
                throw new InvalidOperationException("Logger not initialized.");
            }

            var error = new Error(new Exception("Error Logged"))
            {
                Source = $"{loc}",
                StatusCode = 500,
                Time = DateTime.Now,
                Type = !isSC ? "Error Logged" : "SC Error Logged",
                ApplicationName = "VFX Core CLI",
                Message = loc

            };
            error.MessageLog.Add(new ElmahLogMessageEntry { Level = LogLevel.Error, Message = message, Collapsed = true, TimeStamp = DateTime.Now });
            _errorLog.Log(error);
        }
    }
}
