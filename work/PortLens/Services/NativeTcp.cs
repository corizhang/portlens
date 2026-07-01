using System.Net;
using System.Runtime.InteropServices;

namespace PortLens.Services;

internal static class NativeTcp
{
    private const int AF_INET = 2;
    private const int AF_INET6 = 23;

    public static IReadOnlyList<TcpRow> GetTcpListeners()
    {
        var rows = new List<TcpRow>();
        rows.AddRange(GetTcp4());
        rows.AddRange(GetTcp6());
        return rows;
    }

    private static IEnumerable<TcpRow> GetTcp4()
    {
        var size = 0;
        _ = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TcpTableClass.TCP_TABLE_OWNER_PID_ALL, 0);
        if (size <= 0)
        {
            yield break;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var result = GetExtendedTcpTable(buffer, ref size, true, AF_INET, TcpTableClass.TCP_TABLE_OWNER_PID_ALL, 0);
            if (result != 0)
            {
                yield break;
            }

            var count = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            for (var i = 0; i < count; i++)
            {
                var native = Marshal.PtrToStructure<MibTcpRowOwnerPid>(IntPtr.Add(rowPtr, i * rowSize));
                if (native.State != TcpState.Listen)
                {
                    continue;
                }

                yield return new TcpRow("TCP", new IPAddress(native.LocalAddr).ToString(), NetworkToHostPort(native.LocalPort), "LISTEN", native.OwningPid);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IEnumerable<TcpRow> GetTcp6()
    {
        var size = 0;
        _ = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET6, TcpTableClass.TCP_TABLE_OWNER_PID_ALL, 0);
        if (size <= 0)
        {
            yield break;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var result = GetExtendedTcpTable(buffer, ref size, true, AF_INET6, TcpTableClass.TCP_TABLE_OWNER_PID_ALL, 0);
            if (result != 0)
            {
                yield break;
            }

            var count = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibTcp6RowOwnerPid>();
            for (var i = 0; i < count; i++)
            {
                var native = Marshal.PtrToStructure<MibTcp6RowOwnerPid>(IntPtr.Add(rowPtr, i * rowSize));
                if (native.State != TcpState.Listen)
                {
                    continue;
                }

                var address = new IPAddress(native.LocalAddr, native.ScopeId).ToString();
                yield return new TcpRow("TCP", address, NetworkToHostPort(native.LocalPort), "LISTEN", native.OwningPid);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int NetworkToHostPort(uint port)
    {
        return (int)IPAddress.NetworkToHostOrder((short)port);
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        TcpTableClass tblClass,
        uint reserved);

    private enum TcpTableClass
    {
        TCP_TABLE_OWNER_PID_ALL = 5
    }

    private enum TcpState
    {
        Listen = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public TcpState State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public int OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcp6RowOwnerPid
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LocalAddr;
        public uint LocalScopeId;
        public uint LocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] RemoteAddr;
        public uint RemoteScopeId;
        public uint RemotePort;
        public TcpState State;
        public int OwningPid;

        public long ScopeId => LocalScopeId;
    }
}

internal sealed record TcpRow(string Protocol, string LocalAddress, int LocalPort, string State, int ProcessId);
