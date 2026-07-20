// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.WindowsCredentialManager;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

/// <summary>
/// The real <c>advapi32.dll</c>-backed implementation of <see cref="INativeCredentialApi"/>. Stores
/// secrets as generic credentials persisted for the local machine, i.e. in the vault of the running
/// identity (per-principal isolation). The credential blob holds the UTF-16 encoded secret value.
/// </summary>
[SupportedOSPlatform("windows")]
[ExcludeFromCodeCoverage(Justification = "Thin advapi32 P/Invoke wrapper; verified by the Windows-only integration test rather than unit tests.")]
internal sealed class WindowsCredentialManagerNativeApi : INativeCredentialApi
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public bool TryReadGenericCredential(string targetName, out string? secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        secret = null;
        if (!CredReadW(targetName, CredTypeGeneric, 0, out var credentialPtr))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return false;
            }

            throw new Win32Exception(error, $"Reading the credential '{targetName}' failed.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(credentialPtr);
            secret = ReadBlob(credential);
            return true;
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public void WriteGenericCredential(string targetName, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentNullException.ThrowIfNull(secret);

        var blob = Encoding.Unicode.GetBytes(secret);
        var blobPtr = Marshal.AllocHGlobal(blob.Length == 0 ? 1 : blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);

            var credential = new Credential
            {
                Flags = 0,
                Type = CredTypeGeneric,
                TargetName = targetName,
                Comment = null,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = CredPersistLocalMachine,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = null,
                UserName = targetName
            };

            if (!CredWriteW(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Writing the credential '{targetName}' failed.");
            }
        }
        finally
        {
            // Clear the copied secret bytes from unmanaged and managed memory before releasing.
            Marshal.Copy(new byte[blob.Length == 0 ? 1 : blob.Length], 0, blobPtr, blob.Length == 0 ? 1 : blob.Length);
            Marshal.FreeHGlobal(blobPtr);
            Array.Clear(blob);
        }
    }

    public bool DeleteGenericCredential(string targetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        if (!CredDeleteW(targetName, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return false;
            }

            throw new Win32Exception(error, $"Deleting the credential '{targetName}' failed.");
        }

        return true;
    }

    private static string ReadBlob(Credential credential)
    {
        if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
        {
            return string.Empty;
        }

        var bytes = new byte[credential.CredentialBlobSize];
        Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
        try
        {
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    [SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "The CREDENTIAL structure uses pointer fields and string marshalling that are kept explicit with DllImport for clarity and compatibility.")]
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "The CREDENTIAL structure uses pointer fields and string marshalling that are kept explicit with DllImport for clarity and compatibility.")]
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref Credential credential, uint flags);

    [SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "Kept consistent with the other advapi32 credential imports that require explicit DllImport marshalling.")]
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = "Frees a buffer allocated by CredRead; kept as DllImport alongside the related imports.")]
    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr buffer);

    // CREDENTIALW (see wincred.h). String fields marshal as LPWStr via the struct-level CharSet.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }
}
