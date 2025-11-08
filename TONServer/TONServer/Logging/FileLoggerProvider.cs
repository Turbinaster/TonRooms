using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace TONServer.Logging
{
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logFilePath;
        private readonly LogLevel _minimumLevel;
        private readonly object _writeLock = new object();
        private bool _disposed;

        public FileLoggerProvider(string logFilePath, LogLevel minimumLevel)
        {
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                throw new ArgumentException("Log file path must be provided.", nameof(logFilePath));
            }

            _logFilePath = logFilePath;
            _minimumLevel = minimumLevel;

            var directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FileLoggerProvider));
            }

            return new FileLogger(categoryName, _logFilePath, _minimumLevel, _writeLock);
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private sealed class FileLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly string _logFilePath;
            private readonly LogLevel _minimumLevel;
            private readonly object _writeLock;

            public FileLogger(string categoryName, string logFilePath, LogLevel minimumLevel, object writeLock)
            {
                _categoryName = categoryName;
                _logFilePath = logFilePath;
                _minimumLevel = minimumLevel;
                _writeLock = writeLock;
            }

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel) || formatter == null)
                {
                    return;
                }

                var message = formatter(state, exception);
                if (string.IsNullOrWhiteSpace(message) && exception == null)
                {
                    return;
                }

                var lines = new List<string>();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    lines.Add($"{DateTime.UtcNow:O} [{logLevel}] {_categoryName}: {message}");
                }

                if (exception != null)
                {
                    lines.Add(exception.ToString());
                }

                if (lines.Count == 0)
                {
                    return;
                }

                lock (_writeLock)
                {
                    File.AppendAllText(_logFilePath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
                }
            }
        }
    }
}
