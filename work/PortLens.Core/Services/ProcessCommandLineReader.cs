using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PortLens.Services;

public interface IProcessCommandLineReader
{
    string? Read(int processId, CancellationToken cancellationToken = default);
    IReadOnlyDictionary<int, string?> ReadMany(IReadOnlyCollection<int> processIds, CancellationToken cancellationToken = default);
    void Prune(IEnumerable<int> liveProcessIds);
}

public sealed class ProcessCommandLineReader : IProcessCommandLineReader
{
    private const int ProcessCommandLineInformation = 60;
    private const int ProcessBasicInformationClass = 0;
    private const int PebProcessParametersOffset64 = 0x20;
    private const int CommandLineOffset64 = 0x70;
    private const int UnicodeStringBufferOffset64 = 0x8;

    private static readonly Regex CommandLineRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly ILogger<ProcessCommandLineReader> _logger;
    private readonly ConcurrentDictionary<int, CacheEntry> _cache = new();

    public ProcessCommandLineReader(ILogger<ProcessCommandLineReader> logger)
    {
        _logger = logger;
    }

    public string? Read(int processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryGetCached(processId, out var cached))
        {
            return cached;
        }

        var commandLine = TryReadNative(processId, cancellationToken);
        StoreCache(processId, commandLine);
        return commandLine;
    }

    public IReadOnlyDictionary<int, string?> ReadMany(IReadOnlyCollection<int> processIds, CancellationToken cancellationToken = default)
    {
        if (processIds.Count == 0)
        {
            return new Dictionary<int, string?>();
        }

        var result = new Dictionary<int, string?>();
        var missing = new List<int>();
        foreach (var processId in processIds)
        {
            if (TryGetCached(processId, out var cached))
            {
                result[processId] = cached;
            }
            else
            {
                missing.Add(processId);
            }
        }

        if (missing.Count == 0)
        {
            return result;
        }

        var fetched = new Dictionary<int, string?>();
        foreach (var processId in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var commandLine = TryReadNative(processId, cancellationToken);
            fetched[processId] = commandLine;
            StoreCache(processId, commandLine);
        }

        foreach (var pair in fetched)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    public void Prune(IEnumerable<int> liveProcessIds)
    {
        var live = liveProcessIds.ToHashSet();
        foreach (var key in _cache.Keys.Where(key => !live.Contains(key)).ToList())
        {
            _cache.TryRemove(key, out _);
        }
    }

    private string? TryReadNative(int processId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IntPtr.Size != 8)
        {
            return null;
        }

        var commandLine = TryReadViaProcessCommandLineInformation(processId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(commandLine))
        {
            return commandLine;
        }

        return TryReadViaPeb(processId, cancellationToken);
    }

    private string? TryReadViaProcessCommandLineInformation(int processId, CancellationToken cancellationToken)
    {
        var handle = OpenProcess(ProcessAccessFlags.QueryLimitedInformation | ProcessAccessFlags.VirtualMemoryRead, false, processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var unicode = new UnicodeString();
            var status = NtQueryInformationProcess(
                handle,
                ProcessCommandLineInformation,
                ref unicode,
                Marshal.SizeOf<UnicodeString>(),
                out _);
            if (status != 0 || unicode.Length == 0 || unicode.Buffer == IntPtr.Zero)
            {
                return null;
            }

            var bytes = ReadBytes(handle, unicode.Buffer, unicode.Length);
            return NormalizeCommandLine(Encoding.Unicode.GetString(bytes));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read command line via ProcessCommandLineInformation for process {ProcessId}.", processId);
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private string? TryReadViaPeb(int processId, CancellationToken cancellationToken)
    {
        var handle = OpenProcess(ProcessAccessFlags.QueryLimitedInformation | ProcessAccessFlags.VirtualMemoryRead, false, processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new ProcessBasicInformation();
            var status = NtQueryInformationProcess(
                handle,
                ProcessBasicInformationClass,
                ref info,
                Marshal.SizeOf<ProcessBasicInformation>(),
                out _);
            if (status != 0 || info.PebBaseAddress == IntPtr.Zero)
            {
                return null;
            }

            var parametersAddress = ReadIntPtr(handle, IntPtr.Add(info.PebBaseAddress, PebProcessParametersOffset64));
            if (parametersAddress == IntPtr.Zero)
            {
                return null;
            }

            var commandLineAddress = IntPtr.Add(parametersAddress, CommandLineOffset64);
            var raw = ReadRemoteUnicodeString(handle, commandLineAddress);
            return NormalizeCommandLine(raw);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read command line via PEB for process {ProcessId}.", processId);
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private string? ReadRemoteUnicodeString(IntPtr handle, IntPtr address)
    {
        var length = ReadUInt16(handle, address);
        var bufferAddress = ReadIntPtr(handle, IntPtr.Add(address, UnicodeStringBufferOffset64));
        if (length == 0 || bufferAddress == IntPtr.Zero)
        {
            return null;
        }

        var bytes = ReadBytes(handle, bufferAddress, length);
        return Encoding.Unicode.GetString(bytes);
    }

    private static byte[] ReadBytes(IntPtr handle, IntPtr address, int length)
    {
        var bytes = new byte[length];
        if (!ReadProcessMemory(handle, address, bytes, bytes.Length, out var bytesRead) || bytesRead.ToUInt64() < (ulong)length)
        {
            throw new InvalidOperationException("Unable to read process memory.");
        }

        return bytes;
    }

    private static ushort ReadUInt16(IntPtr handle, IntPtr address)
    {
        var bytes = ReadBytes(handle, address, sizeof(ushort));
        return BitConverter.ToUInt16(bytes, 0);
    }

    private static IntPtr ReadIntPtr(IntPtr handle, IntPtr address)
    {
        var bytes = ReadBytes(handle, address, sizeof(long));
        return new IntPtr(BitConverter.ToInt64(bytes, 0));
    }

    private static string? NormalizeCommandLine(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return CommandLineRegex.Replace(raw.Trim(), " ");
    }

    private bool TryGetCached(int processId, out string? commandLine)
    {
        if (_cache.TryGetValue(processId, out var entry) && !entry.IsExpired)
        {
            commandLine = entry.CommandLine;
            return true;
        }

        commandLine = null;
        return false;
    }

    private void StoreCache(int processId, string? commandLine)
    {
        _cache[processId] = new CacheEntry(commandLine, DateTimeOffset.UtcNow);
    }

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        VirtualMemoryRead = 0x0010,
        QueryLimitedInformation = 0x1000
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(ProcessAccessFlags desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(IntPtr processHandle, IntPtr baseAddress, byte[] buffer, int size, out UIntPtr bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref UnicodeString processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2;
        public IntPtr Reserved3;
        public IntPtr UniqueProcessId;
        public IntPtr Reserved4;
    }

    private sealed class CacheEntry
    {
        public CacheEntry(string? commandLine, DateTimeOffset cachedAt)
        {
            CommandLine = commandLine;
            CachedAt = cachedAt;
        }

        public string? CommandLine { get; }
        public DateTimeOffset CachedAt { get; }
        public bool IsExpired => DateTimeOffset.UtcNow - CachedAt > CacheTtl;
    }
}
