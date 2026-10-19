using System.Security.Cryptography;
using System.Text;

namespace OpenHarness.Client.Core.Authentication;

public sealed class PlatformSecureTokenStorage : ITokenStorage
{
    private readonly string _storagePath;
    private static readonly byte[] Entropy = "OpenHarnessEntropyBytes9928"u8.ToArray();

    public PlatformSecureTokenStorage(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            _storagePath = customPath;
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = Path.Combine(home, ".openharness");
            Directory.CreateDirectory(dir);
            _storagePath = Path.Combine(dir, "token.bin");
        }
    }

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storagePath))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var encrypted = File.ReadAllBytes(_storagePath);
            var decrypted = DecryptSimple(encrypted, Entropy);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(decrypted));
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task SaveRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var rawBytes = Encoding.UTF8.GetBytes(token);
        var encrypted = EncryptSimple(rawBytes, Entropy);

        File.WriteAllBytes(_storagePath, encrypted);

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(_storagePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Best effort file permissions
            }
        }

        return Task.CompletedTask;
    }

    public Task ClearRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_storagePath))
        {
            try
            {
                File.Delete(_storagePath);
            }
            catch
            {
                // Ignore deletion errors
            }
        }

        return Task.CompletedTask;
    }

    private static byte[] EncryptSimple(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = SHA256.HashData(key);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(data, 0, data.Length);

        var result = new byte[aes.IV.Length + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
        return result;
    }

    private static byte[] DecryptSimple(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = SHA256.HashData(key);

        var iv = new byte[aes.BlockSize / 8];
        Buffer.BlockCopy(data, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, iv.Length, data.Length - iv.Length);
    }
}
