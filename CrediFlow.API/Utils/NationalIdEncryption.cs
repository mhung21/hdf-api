using CrediFlow.API.Models;
using System.Security.Cryptography;
using System.Text;

namespace CrediFlow.API.Utils;

/// <summary>
/// Mã hóa AES-256-CBC với HMAC-SHA256 (Encrypt-then-MAC) để bảo vệ dữ liệu cá nhân nhạy cảm (CCCD).
/// Key được lấy từ appsettings.json → section "Encryption:NationalIdKey" (32-byte base64).
///
/// Để sinh key mới: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
/// </summary>
public static class NationalIdEncryption
{
    private static byte[]? _keyBytes;

    private static byte[] GetKey()
    {
        if (_keyBytes != null) return _keyBytes;

        var keyBase64 = Config.Encryption.NationalIdKey;
        if (string.IsNullOrWhiteSpace(keyBase64))
            throw new InvalidOperationException(
                "Encryption:NationalIdKey chưa được cấu hình trong appsettings.json. " +
                "Sinh key: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");

        var key = Convert.FromBase64String(keyBase64);
        if (key.Length != 32)
            throw new InvalidOperationException(
                $"Key trong appsettings.json (Encryption:NationalIdKey) phải là 32 bytes (256-bit) sau khi decode Base64.");

        _keyBytes = key;
        return _keyBytes;
    }

    /// <summary>
    /// Mã hóa CCCD bằng AES-256-CBC.
    /// Output format: Base64(IV || CipherText || HMAC-SHA256)
    /// </summary>
    public static string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;

        var key = GetKey();
        using var aes = Aes.Create();
        aes.KeySize  = 256;
        aes.Mode     = CipherMode.CBC;
        aes.Padding  = PaddingMode.PKCS7;
        aes.Key      = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var ptBytes = Encoding.UTF8.GetBytes(plaintext);
        var ct      = encryptor.TransformFinalBlock(ptBytes, 0, ptBytes.Length);

        // HMAC: tính trên IV || CT để xác thực tính toàn vẹn
        using var hmac  = new HMACSHA256(key);
        var payload     = aes.IV.Concat(ct).ToArray();
        var mac         = hmac.ComputeHash(payload);

        // Kết quả: IV(16) || CT(n) || MAC(32)
        var result = new byte[payload.Length + mac.Length];
        Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
        Buffer.BlockCopy(mac,     0, result, payload.Length, mac.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Giải mã CCCD đã mã hóa. Xác thực HMAC trước khi giải mã (Decrypt-verify-then-decrypt).
    /// </summary>
    public static string Decrypt(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64)) return encryptedBase64;

        try
        {
            var data   = Convert.FromBase64String(encryptedBase64);
            const int ivLen  = 16;
            const int macLen = 32;

            if (data.Length < ivLen + macLen + 1)
                return encryptedBase64; // không phải định dạng đã mã hóa → trả nguyên

            var key     = GetKey();
            var iv      = data[..ivLen];
            var mac     = data[^macLen..];
            var ct      = data[ivLen..^macLen];
            var payload = data[..^macLen];

            // Verify HMAC trước
            using var hmac    = new HMACSHA256(key);
            var expectedMac   = hmac.ComputeHash(payload);
            if (!CryptographicOperations.FixedTimeEquals(mac, expectedMac))
                throw new CryptographicException("HMAC không hợp lệ — dữ liệu có thể bị giả mạo.");

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode    = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key     = key;
            aes.IV      = iv;

            using var decryptor = aes.CreateDecryptor();
            var ptBytes         = decryptor.TransformFinalBlock(ct, 0, ct.Length);
            return Encoding.UTF8.GetString(ptBytes);
        }
        catch (FormatException)
        {
            // Không phải Base64 (dữ liệu cũ chưa mã hóa) → trả nguyên
            return encryptedBase64;
        }
    }

    /// <summary>
    /// Tính SHA-256 hex của CCCD để dùng làm khóa tìm kiếm (equality lookup).
    /// Cùng CCCD → cùng hash → tìm kiếm chính xác vẫn hoạt động.
    /// </summary>
    public static string ComputeSearchHash(string nationalId)
    {
        if (string.IsNullOrEmpty(nationalId)) return nationalId;

        // Hash với HMAC-SHA256 (keyed) để tránh rainbow table
        using var hmac = new HMACSHA256(GetKey());
        var hash       = hmac.ComputeHash(Encoding.UTF8.GetBytes(nationalId.Trim().ToUpper()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
