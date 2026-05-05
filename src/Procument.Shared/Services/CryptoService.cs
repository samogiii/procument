using System.Security.Cryptography;
using System.Text;

namespace Procument.Shared.Services;

public interface ICryptoService
{
    (string CipherText, string EncryptedKey, string Iv, string AuthTag, string Signature) EncryptAndSign(string plainText, string recipientPublicKeyPem, string senderPrivateKeyPem);
    string VerifyAndDecrypt(string cipherText, string encryptedKey, string iv, string authTag, string signature, string senderPublicKeyPem, string recipientPrivateKeyPem);
    (string PrivateKey, string PublicKey) GenerateRsaKeyPair();

    // Browser-compatible AES-GCM (no RSA): Web Crypto appends 16-byte authTag to ciphertext
    string EncryptBrowser(string plainText, string sharedKeyBase64, out string ivBase64);
    string DecryptBrowser(string cipherTextWithTag, string ivBase64, string sharedKeyBase64);
    string GenerateBrowserSharedKey();
}

public class CryptoService : ICryptoService
{
    public (string CipherText, string EncryptedKey, string Iv, string AuthTag, string Signature) EncryptAndSign(string plainText, string recipientPublicKeyPem, string senderPrivateKeyPem)
    {
        // 1. Generate random AES-256 key and IV
        byte[] aesKey = new byte[32];
        byte[] iv = new byte[12]; // GCM standard IV size
        RandomNumberGenerator.Fill(aesKey);
        RandomNumberGenerator.Fill(iv);

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherText = new byte[plainBytes.Length];
        byte[] authTag = new byte[16];

        // 2. Encrypt with AES-GCM
        using (var aesGcm = new AesGcm(aesKey, authTag.Length))
        {
            aesGcm.Encrypt(iv, plainBytes, cipherText, authTag);
        }

        // 3. Encrypt AES key with recipient's RSA Public Key
        byte[] encryptedKey;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(recipientPublicKeyPem);
            encryptedKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
        }

        // 4. Sign the payload (CipherText + Iv + AuthTag + EncryptedKey) with sender's RSA Private Key
        byte[] signature;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(senderPrivateKeyPem);
            byte[] dataToSign = Combine(cipherText, iv, authTag, encryptedKey);
            signature = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        return (
            Convert.ToBase64String(cipherText),
            Convert.ToBase64String(encryptedKey),
            Convert.ToBase64String(iv),
            Convert.ToBase64String(authTag),
            Convert.ToBase64String(signature)
        );
    }

    public string VerifyAndDecrypt(string cipherText, string encryptedKey, string iv, string authTag, string signature, string senderPublicKeyPem, string recipientPrivateKeyPem)
    {
        byte[] cipherBytes = Convert.FromBase64String(cipherText);
        byte[] encryptedKeyBytes = Convert.FromBase64String(encryptedKey);
        byte[] ivBytes = Convert.FromBase64String(iv);
        byte[] authTagBytes = Convert.FromBase64String(authTag);
        byte[] signatureBytes = Convert.FromBase64String(signature);

        // 1. Verify Signature
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(senderPublicKeyPem);
            byte[] dataToVerify = Combine(cipherBytes, ivBytes, authTagBytes, encryptedKeyBytes);
            if (!rsa.VerifyData(dataToVerify, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                throw new CryptographicException("Invalid signature");
            }
        }

        // 2. Decrypt AES Key
        byte[] aesKey;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(recipientPrivateKeyPem);
            aesKey = rsa.Decrypt(encryptedKeyBytes, RSAEncryptionPadding.OaepSHA256);
        }

        // 3. Decrypt CipherText
        byte[] plainBytes = new byte[cipherBytes.Length];
        using (var aesGcm = new AesGcm(aesKey, authTagBytes.Length))
        {
            aesGcm.Decrypt(ivBytes, cipherBytes, authTagBytes, plainBytes);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    public (string PrivateKey, string PublicKey) GenerateRsaKeyPair()
    {
        using (var rsa = RSA.Create(2048))
        {
            return (rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportRSAPublicKeyPem());
        }
    }

    // Accepts base64 OR hex (32-char = 128-bit, 64-char = 256-bit).
    private static byte[] ParseSharedKey(string keyString)
    {
        static bool IsHex(string s) => s.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));
        if ((keyString.Length == 32 || keyString.Length == 64) && IsHex(keyString))
            return Convert.FromHexString(keyString);
        return Convert.FromBase64String(keyString);
    }

    public string EncryptBrowser(string plainText, string sharedKeyBase64, out string ivBase64)
    {
        byte[] key = ParseSharedKey(sharedKeyBase64);
        byte[] iv = new byte[12];
        RandomNumberGenerator.Fill(iv);
        ivBase64 = Convert.ToBase64String(iv);

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] authTag = new byte[16];

        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Encrypt(iv, plainBytes, cipherBytes, authTag);

        byte[] combined = new byte[cipherBytes.Length + 16];
        cipherBytes.CopyTo(combined, 0);
        authTag.CopyTo(combined, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    public string DecryptBrowser(string cipherTextWithTag, string ivBase64, string sharedKeyBase64)
    {
        byte[] key = ParseSharedKey(sharedKeyBase64);
        byte[] combined = Convert.FromBase64String(cipherTextWithTag);
        byte[] ivBytes = Convert.FromBase64String(ivBase64);

        byte[] authTag = combined[^16..];
        byte[] cipherBytes = combined[..^16];

        byte[] plainBytes = new byte[cipherBytes.Length];
        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Decrypt(ivBytes, cipherBytes, authTag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    public string GenerateBrowserSharedKey()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    private static byte[] Combine(params byte[][] arrays)
    {
        byte[] rv = new byte[arrays.Sum(a => a.Length)];
        int offset = 0;
        foreach (byte[] array in arrays)
        {
            System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
            offset += array.Length;
        }
        return rv;
    }
}
