using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Helpers
{
    /// <summary>
    /// Minimal thread-safe file logger to capture startup/runtime diagnostics when running as a GUI app.
    /// </summary>
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly LogLevel _minLevel;
        private readonly object _lock = new();
        private StreamWriter? _writer;
        private bool _disposed;

        public FileLoggerProvider(string filePath, LogLevel minLevel = LogLevel.Information)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

        internal void Write(LogLevel level, string category, EventId eventId, string message, Exception? exception)
        {
            // Skip work if below configured minimum or after disposal to avoid unnecessary I/O.
            if (level < _minLevel || _disposed)
            {
                return;
            }

            try
            {
                EnsureWriter();
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                var sb = new StringBuilder()
                    .Append(timestamp)
                    .Append(' ')
                    .Append(level)
                    .Append(' ')
                    .Append(category)
                    .Append(" [").Append(eventId.Id).Append("] ")
                    .Append(message);

                if (exception != null)
                {
                    sb.Append(" | ").Append(exception);
                }

                lock (_lock)
                {
                    // Serialize writes so multiple threads cannot interleave log lines.
                    _writer!.WriteLine(sb.ToString());
                    _writer.Flush();
                }
            }
            catch
            {
                // Swallow logging errors to avoid crashing the app.
            }
        }

        private void EnsureWriter()
        {
            if (_writer != null)
            {
                return;
            }

            lock (_lock)
            {
                if (_writer == null)
                {
                    // Create directory if it does not exist so logging cannot fail due to missing path.
                    var directory = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // FileShare.ReadWrite allows tailing the log while the app is running.
                    _writer = new StreamWriter(new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        AutoFlush = true
                    };
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        private sealed class FileLogger : ILogger
        {
            private readonly FileLoggerProvider _provider;
            private readonly string _categoryName;

            public FileLogger(FileLoggerProvider provider, string categoryName)
            {
                _provider = provider;
                _categoryName = categoryName;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= _provider._minLevel;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // Fast path exit for filtered levels; avoids formatting cost.
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                // Defer string creation to provided formatter to honor structured logging.
                var message = formatter(state, exception);
                _provider.Write(logLevel, _categoryName, eventId, message, exception);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            // No-op disposable to satisfy ILogger scope requirements without allocations.
            public void Dispose()
            {
            }
        }
    }
}
