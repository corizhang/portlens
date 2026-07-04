namespace PortLens.Models;

public readonly record struct PortEntryKey(string Protocol, string LocalAddress, int LocalPort, int ProcessId);
