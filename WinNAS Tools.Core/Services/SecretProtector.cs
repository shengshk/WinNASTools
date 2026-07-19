using System.Runtime.InteropServices;
using System.Text;

namespace WinNASTools.Core.Services;

/// <summary>当前用户 DPAPI（crypt32），不依赖 NuGet。</summary>
public static class SecretProtector
{
    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        var bytes = Encoding.UTF8.GetBytes(plain);
        var protectedBytes = ProtectBytes(bytes);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64)) return "";
        try
        {
            var protectedBytes = Convert.FromBase64String(protectedBase64);
            var bytes = UnprotectBytes(protectedBytes);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }

    private static byte[] ProtectBytes(byte[] data)
    {
        var blobIn = new DATA_BLOB();
        var blobOut = new DATA_BLOB();
        try
        {
            blobIn.cbData = data.Length;
            blobIn.pbData = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, blobIn.pbData, data.Length);
            if (!CryptProtectData(ref blobIn, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref blobOut))
                throw new InvalidOperationException("CryptProtectData 失败。");
            var result = new byte[blobOut.cbData];
            Marshal.Copy(blobOut.pbData, result, 0, blobOut.cbData);
            return result;
        }
        finally
        {
            if (blobIn.pbData != IntPtr.Zero) Marshal.FreeHGlobal(blobIn.pbData);
            if (blobOut.pbData != IntPtr.Zero) Marshal.FreeHGlobal(blobOut.pbData);
        }
    }

    private static byte[] UnprotectBytes(byte[] data)
    {
        var blobIn = new DATA_BLOB();
        var blobOut = new DATA_BLOB();
        try
        {
            blobIn.cbData = data.Length;
            blobIn.pbData = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, blobIn.pbData, data.Length);
            if (!CryptUnprotectData(ref blobIn, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref blobOut))
                throw new InvalidOperationException("CryptUnprotectData 失败。");
            var result = new byte[blobOut.cbData];
            Marshal.Copy(blobOut.pbData, result, 0, blobOut.cbData);
            return result;
        }
        finally
        {
            if (blobIn.pbData != IntPtr.Zero) Marshal.FreeHGlobal(blobIn.pbData);
            if (blobOut.pbData != IntPtr.Zero) Marshal.FreeHGlobal(blobOut.pbData);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn, string? szDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn, StringBuilder? ppszDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);
}
