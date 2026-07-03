using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PortLens.Services;

internal static class ProcessCommandLineReader
{
    private static readonly Regex CommandLineRegex = new(@"\s+", RegexOptions.Compiled);

    public static string? Read(int processId, CancellationToken cancellationToken = default)
    {
        return Safe(() =>
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"(Get-CimInstance Win32_Process -Filter 'ProcessId={processId}').CommandLine\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            using (cancellationToken.Register(() => Safe(() => process.Kill(entireProcessTree: true))))
            {
                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(350))
                {
                    Safe(() =>
                    {
                        process.Kill(entireProcessTree: true);
                    });
                    return null;
                }

                var trimmed = output.Trim();
                return string.IsNullOrWhiteSpace(trimmed)
                    ? null
                    : CommandLineRegex.Replace(trimmed, " ");
            }
        });
    }

    public static IReadOnlyDictionary<int, string?> ReadMany(IReadOnlyCollection<int> processIds, CancellationToken cancellationToken = default)
    {
        if (processIds.Count == 0)
        {
            return new Dictionary<int, string?>();
        }

        return Safe(() =>
        {
            var filter = BuildProcessIdFilter(processIds);
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_Process -Filter \\\"{filter}\\\" | Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new Dictionary<int, string?>();
            }

            using (cancellationToken.Register(() => Safe(() => process.Kill(entireProcessTree: true))))
            {
                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(1200))
                {
                    Safe(() =>
                    {
                        process.Kill(entireProcessTree: true);
                    });
                    return new Dictionary<int, string?>();
                }

                cancellationToken.ThrowIfCancellationRequested();
                return ParseJsonProcessOutput(output, processIds);
            }
        }) ?? new Dictionary<int, string?>();
    }

    private static string BuildProcessIdFilter(IEnumerable<int> processIds)
    {
        return string.Join(" OR ", processIds.Select(processId => $"ProcessId={processId}"));
    }

    private static IReadOnlyDictionary<int, string?> ParseJsonProcessOutput(string output, IReadOnlyCollection<int> processIds)
    {
        var wanted = processIds.ToHashSet();
        var result = new Dictionary<int, string?>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return result;
        }

        using var document = JsonDocument.Parse(output);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                AddProcess(element, wanted, result);
            }
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            AddProcess(document.RootElement, wanted, result);
        }

        return result;
    }

    private static void AddProcess(JsonElement element, HashSet<int> wanted, Dictionary<int, string?> result)
    {
        if (!element.TryGetProperty("ProcessId", out var pidElement) || !pidElement.TryGetInt32(out var processId) || !wanted.Contains(processId))
        {
            return;
        }

        var commandLine = element.TryGetProperty("CommandLine", out var commandLineElement) && commandLineElement.ValueKind == JsonValueKind.String
            ? CommandLineRegex.Replace(commandLineElement.GetString() ?? "", " ").Trim()
            : null;
        result[processId] = string.IsNullOrWhiteSpace(commandLine) ? null : commandLine;
    }

    private static T? Safe<T>(Func<T?> action)
    {
        try
        {
            return action();
        }
        catch
        {
            return default;
        }
    }

    private static void Safe(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // ignored
        }
    }
}
