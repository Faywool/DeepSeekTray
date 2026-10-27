using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DeepSeekTray.Services;

/// <summary>
/// 用 Windows DPAPI（当前用户范围）加密 API Key，避免明文落盘。
/// 直接 P/Invoke，不额外引入 NuGet 包。
/// </summary>
public static class Dpapi
{
    private const int CryptProtectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    public static string Protect(string plainText)
    {
        var input = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
        var blobIn = new DataBlob { Size = input.Length, Data = Marshal.AllocHGlobal(input.Length) };
        try
        {
            Marshal.Copy(input, 0, blobIn.Data, input.Length);

            if (!CryptProtectData(ref blobIn, "DeepSeekTray.ApiKey", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, out var blobOut))
            {
                throw new InvalidOperationException("DPAPI 加密失败，错误码：" + Marshal.GetLastWin32Error());
            }

            try
            {
                var encrypted = new byte[blobOut.Size];
                Marshal.Copy(blobOut.Data, encrypted, 0, blobOut.Size);
                return Convert.ToBase64String(encrypted);
            }
            finally
            {
                LocalFree(blobOut.Data);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobIn.Data);
        }
    }

    public static string? Unprotect(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        byte[] encrypted;
        try
        {
            encrypted = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }

        var blobIn = new DataBlob { Size = encrypted.Length, Data = Marshal.AllocHGlobal(encrypted.Length) };
        try
        {
            Marshal.Copy(encrypted, 0, blobIn.Data, encrypted.Length);

            if (!CryptUnprotectData(ref blobIn, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, out var blobOut))
            {
                return null;
            }

            try
            {
                var plain = new byte[blobOut.Size];
                Marshal.Copy(blobOut.Data, plain, 0, blobOut.Size);
                return Encoding.UTF8.GetString(plain);
            }
            finally
            {
                LocalFree(blobOut.Data);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobIn.Data);
        }
    }
}
