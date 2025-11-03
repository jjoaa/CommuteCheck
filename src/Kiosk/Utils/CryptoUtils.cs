using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Kiosk.Utils;
using Newtonsoft.Json.Linq;

public static class CryptoUtils
{
    private static readonly string RTDB_SECRET = Define.RTDB_SECRET_KEY;
    private static readonly string RTDB_IV = Define.RTDB_IV;

    /* RSA 암호화 (OAEP SHA-1) */
    public static string EncryptWithPublicKey(string text, string publicKey)
    {
        try
        {
            string keyClean = publicKey
                .Replace("-----BEGIN PUBLIC KEY-----", "")
                .Replace("-----END PUBLIC KEY-----", "")
                .Replace("\r", "")
                .Replace("\n", "");

            byte[] keyBytes = Convert.FromBase64String(keyClean);
            byte[] data = Encoding.UTF8.GetBytes(text);

            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
            byte[] encrypted = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA1);
            return Convert.ToBase64String(encrypted);
        }
        catch (Exception ex)
        {
            throw new Exception($"RSA 암호화 중 오류 발생: {ex.Message}");
        }
    }

    /* AES/CBC/PKCS5Padding 암호화 */
    public static string Encrypt(string plainText)
    {
        var keyBytes = Encoding.UTF8.GetBytes(RTDB_SECRET);
        var ivBytes = Encoding.UTF8.GetBytes(RTDB_IV);
        ValidateKeyIv(keyBytes, ivBytes);

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.IV = ivBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    /* AES/CBC/PKCS5Padding 복호화 */
    public static string Decrypt(string base64Cipher)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(base64Cipher)) return null;

            // 흔한 변형 보정: 공백/개행/URL-safe/HTML-escape 후 Base64 
            var cleaned = base64Cipher.Trim()
                .Replace(" ", "") // 간혹 공백 섞임
                .Replace("\r", "")
                .Replace("\n", "");
            cleaned = cleaned.Replace('-', '+').Replace('_', '/');
            //패딩보정
            cleaned = cleaned.PadRight(cleaned.Length + (4 - cleaned.Length % 4) % 4, '=');

            byte[] cipherBytes;
            try
            {
                cipherBytes = Convert.FromBase64String(cleaned);
            }
            catch
            {
                return null;
            }

            // 키/IV 길이 보정: AES-256(32바이트 키), IV 16바이트
            byte[] keyBytes = Encoding.UTF8.GetBytes(RTDB_SECRET ?? "");
            byte[] ivBytes = Encoding.UTF8.GetBytes(RTDB_IV ?? "");
            if (keyBytes.Length != 32)
            {
                // 길이가 안 맞으면 SHA-256으로 32바이트 파생
                using var sha = SHA256.Create();
                keyBytes = sha.ComputeHash(keyBytes);
            }

            if (ivBytes.Length != 16)
            {
                // IV는 16바이트 필요 — SHA-256 후 앞 16바이트 사용
                using var sha = SHA256.Create();
                ivBytes = sha.ComputeHash(ivBytes).AsSpan(0, 16).ToArray();
            }

            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(cipherBytes, writable: false))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    using var sr = new StreamReader(cs, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
                    try
                    {
                        return sr.ReadToEnd();
                    }
                    catch (CryptographicException)
                    {
                        return null;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Decrypt] 오류: {ex}");
            return null;
        }
    }

    // JSON 전용 헬퍼
    public static string EncryptRequest(JObject request)
    {
        var wrapped = new JObject { ["request"] = request };
        return Encrypt(wrapped.ToString(Newtonsoft.Json.Formatting.None));
    }

    private static void ValidateKeyIv(byte[] keyBytes, byte[] ivBytes)
    {
        if (!(keyBytes.Length == 16 || keyBytes.Length == 24 || keyBytes.Length == 32))
            throw new ArgumentException("AES key must be 16, 24, or 32 bytes (UTF-8).");
        if (ivBytes.Length != 16)
            throw new ArgumentException("AES IV must be 16 bytes (UTF-8).");
    }
}