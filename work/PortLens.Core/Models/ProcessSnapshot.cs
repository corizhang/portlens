namespace PortLens.Models;

public readonly record struct ProcessSnapshot(
    int Id,
    string ProcessName,
    DateTime StartTime,
    long WorkingSet64,
    TimeSpan TotalProcessorTime,
    string? ExecutablePath);
