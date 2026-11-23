using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyceCode.Services;

public sealed class WindowsCredentialStore : ISecretStore
{
    private const uint GenericCredentialType = 1;
    private const uint LocalMachinePersistence = 2;
    private const string Prefix = "KeyceCode/";

    public string? Get(string name)
    {
        if (!CredRead(Target(name), GenericCredentialType, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168) return null;
            throw new Win32Exception(error, "Lecture du Gestionnaire d’identifiants Windows impossible.");
        }
        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return null;
            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        }
        finally { CredFree(pointer); }
    }

    public void Set(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { Delete(name); return; }
        var targetPointer = Marshal.StringToCoTaskMemUni(Target(name));
        var userPointer = Marshal.StringToCoTaskMemUni("KEYCE CODE");
        var bytes = System.Text.Encoding.Unicode.GetBytes(value);
        var blobPointer = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blobPointer, bytes.Length);
            var credential = new NativeCredential
            {
                Type = GenericCredentialType,
                TargetName = targetPointer,
                CredentialBlob = blobPointer,
                CredentialBlobSize = (uint)bytes.Length,
                Persist = LocalMachinePersistence,
                UserName = userPointer
            };
            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Enregistrement sécurisé impossible.");
        }
        finally
        {
            Marshal.FreeCoTaskMem(targetPointer);
            Marshal.FreeCoTaskMem(userPointer);
            Marshal.FreeCoTaskMem(blobPointer);
        }
    }

    public void Delete(string name)
    {
        if (!CredDelete(Target(name), GenericCredentialType, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1168) throw new Win32Exception(error, "Suppression sécurisée impossible.");
        }
    }

    public static string Target(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsWhiteSpace) || name.Contains('/', StringComparison.Ordinal))
            throw new ArgumentException("Nom de secret invalide.", nameof(name));
        return Prefix + name;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CredFree(IntPtr credential);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
