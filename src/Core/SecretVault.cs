using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FrenMits;

// Encrypts small secrets with Windows DPAPI (current-user scope) so they never
// sit on disk in plaintext. The ciphertext only decrypts on the same Windows
// account it was made on, so a copied, shared, synced, or stolen config file
// gives up nothing. P/Invoked directly so we don't ship another assembly.
internal static class SecretVault
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptProtectData(ref Blob dataIn, IntPtr descr, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob dataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(ref Blob dataIn, IntPtr descr, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr mem);

    // Never pop a system prompt from inside the game.
    private const int UiForbidden = 0x1;

    // App-specific entropy: not a secret, just keeps generic DPAPI blobs from
    // other apps (and ours from them) from cross-decrypting by accident.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FrenMits.SecretVault.v1");

    // Plaintext -> base64 ciphertext ("" stays "").
    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        try
        {
            var output = Transform(Encoding.UTF8.GetBytes(plain), protect: true);
            return output == null ? "" : Convert.ToBase64String(output);
        }
        catch { return ""; }
    }

    // Base64 ciphertext -> plaintext. Anything unreadable (different Windows
    // account, corrupt blob) comes back "", which the UI treats as "not set up
    // yet" so the user just re-enters it.
    public static string Unprotect(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return "";
        try
        {
            var output = Transform(Convert.FromBase64String(cipher), protect: false);
            return output == null ? "" : Encoding.UTF8.GetString(output);
        }
        catch { return ""; }
    }

    private static byte[]? Transform(byte[] input, bool protect)
    {
        var inPtr = Marshal.AllocHGlobal(input.Length);
        var entPtr = Marshal.AllocHGlobal(Entropy.Length);
        var outBlob = default(Blob);
        try
        {
            Marshal.Copy(input, 0, inPtr, input.Length);
            Marshal.Copy(Entropy, 0, entPtr, Entropy.Length);
            var inBlob = new Blob { Length = input.Length, Data = inPtr };
            var entBlob = new Blob { Length = Entropy.Length, Data = entPtr };
            var ok = protect
                ? CryptProtectData(ref inBlob, IntPtr.Zero, ref entBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref outBlob)
                : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref outBlob);
            if (!ok || outBlob.Data == IntPtr.Zero) return null;
            var result = new byte[outBlob.Length];
            Marshal.Copy(outBlob.Data, result, 0, outBlob.Length);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(inPtr);
            Marshal.FreeHGlobal(entPtr);
            // DPAPI allocates the output; the docs say free it with LocalFree.
            if (outBlob.Data != IntPtr.Zero) LocalFree(outBlob.Data);
        }
    }
}
