﻿using ElmahCore;

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
        public static void LogEvent()
        {
            if (_errorLog == null)
            {
                throw new InvalidOperationException("Logger not initialized.");
            }

            var error = new Error(new Exception("Test error"));
            _errorLog.Log(error);
        }
        public static void LogInfo(string message, string loc)
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
                Type = "Info Logged",
                ApplicationName = "VFX Core CLI",
                Message = loc
            };
            error.MessageLog.Add(new ElmahLogMessageEntry { Level = LogLevel.None, Message = message, Collapsed = true, TimeStamp = DateTime.Now });
            _errorLog.Log(error);
        }

        public static void LogError(string message, string loc)
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
                Type = "Error Logged",
                ApplicationName = "VFX Core CLI",
                Message = loc

            };
            error.MessageLog.Add(new ElmahLogMessageEntry { Level = LogLevel.Error, Message = message, Collapsed = true, TimeStamp = DateTime.Now });
            _errorLog.Log(error);
        }
    }
}
