using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PortLens.Services;

internal static class ProcessCurrentDirectoryReader
{
    private const int ProcessBasicInformationClass = 0;
    private const int PebProcessParametersOffset64 = 0x20;
    private const int CurrentDirectoryOffset64 = 0x38;
    private const int UnicodeStringBufferOffset64 = 0x8;

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        VirtualMemoryRead = 0x0010,
        QueryLimitedInformation = 0x1000
    }

    public static string? Read(int processId)
    {
        if (IntPtr.Size != 8)
        {
            return null;
        }

        var handle = OpenProcess(ProcessAccessFlags.QueryLimitedInformation | ProcessAccessFlags.VirtualMemoryRead, false, processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var info = new ProcessBasicInformation();
            var status = NtQueryInformationProcess(handle, ProcessBasicInformationClass, ref info, Marshal.SizeOf<ProcessBasicInformation>(), out _);
            if (status != 0 || info.PebBaseAddress == IntPtr.Zero)
            {
                return null;
            }

            var parametersAddress = ReadIntPtr(handle, IntPtr.Add(info.PebBaseAddress, PebProcessParametersOffset64));
            if (parametersAddress == IntPtr.Zero)
            {
                return null;
            }

            var currentDirectoryAddress = IntPtr.Add(parametersAddress, CurrentDirectoryOffset64);
            var length = ReadUInt16(handle, currentDirectoryAddress);
            var bufferAddress = ReadIntPtr(handle, IntPtr.Add(currentDirectoryAddress, UnicodeStringBufferOffset64));
            if (length == 0 || bufferAddress == IntPtr.Zero)
            {
                return null;
            }

            var bytes = ReadBytes(handle, bufferAddress, length);
            var path = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            return Directory.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
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

    private static byte[] ReadBytes(IntPtr handle, IntPtr address, int length)
    {
        var bytes = new byte[length];
        if (!ReadProcessMemory(handle, address, bytes, bytes.Length, out var bytesRead) || bytesRead.ToUInt64() < (ulong)length)
        {
            throw new InvalidOperationException("Unable to read process memory.");
        }

        return bytes;
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
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);

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
}
