using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace PortLens.Services;

/// <summary>
/// Captures a one-time snapshot of running processes using NtQuerySystemInformation.
/// This avoids spawning external processes or using WMI to enumerate process relationships.
/// </summary>
internal sealed class NativeProcessSnapshot
{
    private const int SystemProcessInformation = 5;

    // Offsets for SYSTEM_PROCESS_INFORMATION on x64.
    private const int NextEntryOffsetOffset = 0;
    private const int UniqueProcessIdOffset = 0x38;
    private const int InheritedFromUniqueProcessIdOffset = 0x40;
    private const int ImageNameOffset = 0x5c;

    private NativeProcessSnapshot(
        IReadOnlyDictionary<int, IReadOnlyList<int>> childrenByParent,
        IReadOnlyDictionary<int, int> parentByChild,
        IReadOnlyDictionary<int, string> processNames)
    {
        ChildrenByParent = childrenByParent;
        ParentByChild = parentByChild;
        ProcessNames = processNames;
    }

    public IReadOnlyDictionary<int, IReadOnlyList<int>> ChildrenByParent { get; }

    public IReadOnlyDictionary<int, int> ParentByChild { get; }

    public IReadOnlyDictionary<int, string> ProcessNames { get; }

    public static NativeProcessSnapshot? Capture()
    {
        if (IntPtr.Size != 8)
        {
            return null;
        }

        var buffer = RentAndCapture(out var length);
        if (buffer == IntPtr.Zero || length <= 0)
        {
            return null;
        }

        try
        {
            return Parse(buffer, length);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IntPtr RentAndCapture(out int length)
    {
        length = 0;
        var size = 64 * 1024;
        var attempts = 0;
        while (attempts < 8)
        {
            var buffer = Marshal.AllocHGlobal(size);
            var status = NtQuerySystemInformation(SystemProcessInformation, buffer, size, out length);
            if (status == 0)
            {
                return buffer;
            }

            Marshal.FreeHGlobal(buffer);
            if (unchecked((uint)status) != 0xC0000004) // STATUS_INFO_LENGTH_MISMATCH
            {
                return IntPtr.Zero;
            }

            size = length > size ? length : size * 2;
            attempts++;
        }

        return IntPtr.Zero;
    }

    private static NativeProcessSnapshot Parse(IntPtr buffer, int length)
    {
        var childrenByParent = new Dictionary<int, List<int>>();
        var parentByChild = new Dictionary<int, int>();
        var processNames = new Dictionary<int, string>();

        var bytes = new byte[length];
        Marshal.Copy(buffer, bytes, 0, length);

        var offset = 0;
        while (offset < length)
        {
            var nextEntryOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + NextEntryOffsetOffset));
            var processId = (int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset + UniqueProcessIdOffset));
            var parentProcessId = (int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset + InheritedFromUniqueProcessIdOffset));

            if (processId > 0)
            {
                processNames[processId] = ReadImageName(bytes, offset + ImageNameOffset);
                parentByChild[processId] = parentProcessId;
                if (parentProcessId > 0)
                {
                    if (!childrenByParent.TryGetValue(parentProcessId, out var children))
                    {
                        children = new List<int>();
                        childrenByParent[parentProcessId] = children;
                    }

                    children.Add(processId);
                }
            }

            if (nextEntryOffset == 0)
            {
                break;
            }

            offset += nextEntryOffset;
        }

        return new NativeProcessSnapshot(
            childrenByParent.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value),
            parentByChild,
            processNames);
    }

    private static string ReadImageName(byte[] bytes, int offset)
    {
        // ImageName is a UNICODE_STRING at this offset.
        var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
        var bufferAddress = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset + 0x08));
        if (length == 0 || bufferAddress == 0)
        {
            return string.Empty;
        }

        var nameBytes = new byte[length];
        Marshal.Copy(new IntPtr(bufferAddress), nameBytes, 0, length);
        return Encoding.Unicode.GetString(nameBytes);
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(
        int systemInformationClass,
        IntPtr systemInformation,
        int systemInformationLength,
        out int returnLength);
}
