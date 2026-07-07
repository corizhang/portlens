using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PortLens.Desktop.Services;

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();
    private readonly Task _writeTask;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public FileLoggerProvider(string path)
    {
        _path = path;
        _writeTask = Task.Factory.StartNew(
            () => WriteLoop(_cts.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _channel.Writer);
    }

    /// <summary>
    /// Signals the background writer to stop and waits for queued messages to be flushed.
    /// Safe to call multiple times.
    /// </summary>
    public void Flush(TimeSpan timeout)
    {
        if (_disposed)
        {
            return;
        }

        _channel.Writer.TryComplete();
        try
        {
            _writeTask.Wait(timeout);
        }
        catch
        {
            // Best-effort flush during shutdown or crash.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _channel.Writer.TryComplete();
        try
        {
            _writeTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Logging must not throw during shutdown.
        }

        _cts.Dispose();
    }

    private async Task WriteLoop(CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = new FileStream(
                _path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous);
            using var writer = new StreamWriter(stream) { AutoFlush = false };

            while (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var batch = new List<string>(100);
                while (batch.Count < 100 && _channel.Reader.TryRead(out var line))
                {
                    batch.Add(line);
                }

                if (batch.Count == 0)
                {
                    continue;
                }

                foreach (var line in batch)
                {
                    await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
                }

                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch
        {
            // Logging must not throw; dropping remaining messages is preferable to crashing.
        }
        finally
        {
            // Drain anything that arrived after cancellation or an unexpected error.
            try
            {
                await using var stream = new FileStream(
                    _path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.Asynchronous);
                using var writer = new StreamWriter(stream) { AutoFlush = true };
                while (_channel.Reader.TryRead(out var line))
                {
                    await writer.WriteLineAsync(line.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
                // Best effort.
            }
        }
    }
}

internal sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly ChannelWriter<string> _writer;

    public FileLogger(string categoryName, ChannelWriter<string> writer)
    {
        _categoryName = categoryName;
        _writer = writer;
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
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        // Unbounded channel: best-effort enqueue; never block the caller.
        _writer.TryWrite(line);
    }
}
