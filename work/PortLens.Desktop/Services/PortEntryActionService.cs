using System.Diagnostics;
using PortLens.Desktop.Dialogs;
using PortLens.Desktop.ViewModels;
using PortLens.Services;

namespace PortLens.Desktop.Services;

internal sealed class PortEntryActionService
{
    private readonly PortScanner _scanner;
    private readonly Action<string> _notify;
    private readonly Func<Task> _refreshAsync;

    public PortEntryActionService(PortScanner scanner, Action<string> notify, Func<Task> refreshAsync)
    {
        _scanner = scanner;
        _notify = notify;
        _refreshAsync = refreshAsync;
    }

    public void OpenUrl(PortEntryViewModel entry)
    {
        TryStart(new ProcessStartInfo(entry.Url) { UseShellExecute = true }, $"Opened {entry.Url}", "Open URL failed.");
    }

    public void CopyUrl(PortEntryViewModel entry)
    {
        CopyText(entry.Url, $"Copied {entry.Url}");
    }

    public void CopyPid(PortEntryViewModel entry)
    {
        CopyText(entry.ProcessId.ToString(), $"Copied PID {entry.ProcessId}");
    }

    public void CopyCommandLine(PortEntryViewModel entry)
    {
        CopyText(entry.CommandText, "Copied command line.");
    }

    public void OpenProcessDirectory(PortEntryViewModel entry)
    {
        var directory = entry.ProcessDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            _notify("Process directory is unavailable.");
            return;
        }

        OpenDirectory(directory, "Process directory does not exist.");
    }

    public void OpenProjectDirectory(PortEntryViewModel entry)
    {
        if (string.IsNullOrWhiteSpace(entry.WorkingDirectory))
        {
            _notify("Project directory is unavailable.");
            return;
        }

        OpenDirectory(entry.WorkingDirectory, "Project directory does not exist.");
    }

    public void OpenTerminal(PortEntryViewModel entry)
    {
        var directory = !string.IsNullOrWhiteSpace(entry.WorkingDirectory)
            ? entry.WorkingDirectory
            : entry.ProcessDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            _notify("Terminal directory is unavailable.");
            return;
        }

        if (TryStart(new ProcessStartInfo("wt.exe")
            {
                UseShellExecute = true,
                WorkingDirectory = directory
            },
            "Opened terminal.",
            null))
        {
            return;
        }

        TryStart(new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = true,
            WorkingDirectory = directory
        }, "Opened terminal.", "Open terminal failed.");
    }

    public async Task KillProcessTreeAsync(PortEntryViewModel entry)
    {
        var childProcessCount = await Task.Run(() => _scanner.CountChildProcesses(entry.ProcessId));
        var confirmed = await KillConfirmationDialog.ShowAsync(entry, childProcessCount);
        if (!confirmed)
        {
            return;
        }

        try
        {
            _scanner.Kill(entry.ProcessId);
            _notify($"Killed PID {entry.ProcessId}.");
            _ = _refreshAsync();
        }
        catch (Exception ex)
        {
            _notify($"Kill failed: {ex.Message}");
        }
    }

    private void OpenDirectory(string directory, string missingMessage)
    {
        if (!Directory.Exists(directory))
        {
            _notify(missingMessage);
            return;
        }

        TryStart(new ProcessStartInfo(directory) { UseShellExecute = true }, "Opened directory.", "Open directory failed.");
    }

    private void CopyText(string text, string successMessage)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            _notify(successMessage);
        }
        catch (Exception ex)
        {
            _notify($"Copy failed: {ex.Message}");
        }
    }

    private bool TryStart(ProcessStartInfo startInfo, string successMessage, string? failureMessage)
    {
        try
        {
            Process.Start(startInfo);
            _notify(successMessage);
            return true;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                _notify($"{failureMessage} {ex.Message}");
            }

            return false;
        }
    }
}
