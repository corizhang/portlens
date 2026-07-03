using System.Diagnostics;
using System.Text.Json;

namespace PortLens.Services;

internal static class ProcessTreeReader
{
    public static int CountDescendants(int processId)
    {
        return Safe(() =>
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId | ConvertTo-Json -Compress\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return 0;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(1200))
            {
                Safe(() =>
                {
                    process.Kill(entireProcessTree: true);
                });
                return 0;
            }

            return CountDescendantsFromJson(output, processId);
        });
    }

    private static int CountDescendantsFromJson(string output, int rootProcessId)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return 0;
        }

        var childrenByParent = new Dictionary<int, List<int>>();
        using var document = JsonDocument.Parse(output);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                AddProcess(element, childrenByParent);
            }
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            AddProcess(document.RootElement, childrenByParent);
        }

        var count = 0;
        var stack = new Stack<int>();
        stack.Push(rootProcessId);
        while (stack.Count > 0)
        {
            var parent = stack.Pop();
            if (!childrenByParent.TryGetValue(parent, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                count++;
                stack.Push(child);
            }
        }

        return count;
    }

    private static void AddProcess(JsonElement element, Dictionary<int, List<int>> childrenByParent)
    {
        if (!element.TryGetProperty("ProcessId", out var pidElement) ||
            !pidElement.TryGetInt32(out var processId) ||
            !element.TryGetProperty("ParentProcessId", out var parentElement) ||
            !parentElement.TryGetInt32(out var parentProcessId))
        {
            return;
        }

        if (!childrenByParent.TryGetValue(parentProcessId, out var children))
        {
            children = new List<int>();
            childrenByParent[parentProcessId] = children;
        }

        children.Add(processId);
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
