using Microsoft.Extensions.Logging;

namespace PortLens.Desktop.Services;

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;

    public FileLoggerProvider(string path)
    {
        _path = path;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _path);
    }

    public void Dispose()
    {
    }
}

internal sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _path;
    private static readonly object Lock = new();

    public FileLogger(string categoryName, string path)
    {
        _categoryName = categoryName;
        _path = path;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Warning;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {_categoryName}: {message}";
        lock (Lock)
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(_path, line + Environment.NewLine);
                if (exception is not null)
                {
                    File.AppendAllText(_path, exception.ToString() + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must not throw.
            }
        }
    }
}
