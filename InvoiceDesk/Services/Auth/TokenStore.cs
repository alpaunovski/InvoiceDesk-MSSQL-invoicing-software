using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using InvoiceDesk.Models;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Services.Auth;

public interface ITokenStore
{
    Task SaveAsync(SessionToken token);
    Task<SessionToken?> GetAsync();
    Task ClearAsync();
}

/// <summary>
/// Stores the auth token in Windows Credential Manager with a DPAPI-encrypted file fallback.
/// </summary>
public sealed class TokenStore : ITokenStore
{
    private const string TargetName = "InvoiceDeskAuthToken";
    private readonly ILogger<TokenStore> _logger;
    private readonly string _fallbackPath;

    public TokenStore(ILogger<TokenStore> logger)
    {
        _logger = logger;
        _fallbackPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvoiceDesk", "token.dat");
    }

    public async Task SaveAsync(SessionToken token)
    {
        var payload = JsonSerializer.Serialize(token);
        if (!TryWriteToCredentialManager(payload))
        {
            _logger.LogWarning("Falling back to DPAPI token store");
            await WriteFallbackAsync(payload);
        }
    }

    public async Task<SessionToken?> GetAsync()
    {
        var payload = ReadFromCredentialManager();
        if (string.IsNullOrWhiteSpace(payload))
        {
            payload = await ReadFallbackAsync();
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SessionToken>(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse stored token");
            return null;
        }
    }

    public async Task ClearAsync()
    {
        DeleteCredential();
        if (File.Exists(_fallbackPath))
        {
            try
            {
                File.Delete(_fallbackPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete fallback token file");
            }
        }
        await Task.CompletedTask;
    }

    private bool TryWriteToCredentialManager(string secret)
    {
        try
        {
            var blob = Encoding.Unicode.GetBytes(secret);
            var blobPtr = Marshal.AllocCoTaskMem(blob.Length);
            Marshal.Copy(blob, 0, blobPtr, blob.Length);

            var credential = new NativeCredential
            {
                Type = 1, // CRED_TYPE_GENERIC
                TargetName = TargetName,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = 2, // CRED_PERSIST_LOCAL_MACHINE
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = null,
                UserName = Environment.UserName,
            };

            var result = CredWrite(ref credential, 0);
            Marshal.FreeCoTaskMem(blobPtr);
            if (!result)
            {
                _logger.LogWarning("CredWrite failed with {Error}", Marshal.GetLastWin32Error());
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CredWrite exception");
            return false;
        }
    }

    private string? ReadFromCredentialManager()
    {
        try
        {
            if (!CredRead(TargetName, 1, 0, out var credPtr))
            {
                return null;
            }

            using var handle = new CriticalCredentialHandle(credPtr);
            var cred = handle.GetCredential();
            if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(cred.CredentialBlob, (int)cred.CredentialBlobSize / 2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CredRead exception");
            return null;
        }
    }

    private void DeleteCredential()
    {
        CredDelete(TargetName, 1, 0);
    }

    private async Task WriteFallbackAsync(string payload)
    {
        try
        {
            var directory = Path.GetDirectoryName(_fallbackPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(payload), null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(_fallbackPath, encrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write fallback token");
        }
    }

    private async Task<string?> ReadFallbackAsync()
    {
        if (!File.Exists(_fallbackPath))
        {
            return null;
        }

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_fallbackPath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read fallback token");
            return null;
        }
    }

    #region Native interop

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string? TargetName;
        public string? Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite([In] ref NativeCredential userCredential, [In] uint flags);

    [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32", SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32", SetLastError = true)]
    private static extern void CredFree([In] IntPtr buffer);

    private sealed class CriticalCredentialHandle : CriticalHandle
    {
        public CriticalCredentialHandle(IntPtr preexistingHandle) : base(IntPtr.Zero)
        {
            SetHandle(preexistingHandle);
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        public NativeCredential GetCredential()
        {
            if (IsInvalid)
            {
                throw new InvalidOperationException("Invalid CriticalHandle");
            }

            return Marshal.PtrToStructure<NativeCredential>(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                CredFree(handle);
                SetHandle(IntPtr.Zero);
                return true;
            }
            return false;
        }
    }

    #endregion
}
