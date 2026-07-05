using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Shared;

public static class SecureMessageCrypto
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static RSA LoadPrivateKeyFromFile(string path)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        return rsa;
    }

    public static RSA LoadPublicKeyFromFile(string path)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        return rsa;
    }

    public static RSA LoadPublicKeyFromPem(string pem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }

    public static SecureEnvelopeDto EncryptAndSign(
        string sensorId,
        MeasurementPayloadDto payload,
        RSA serverPublicKey,
        RSA sensorPrivateKey)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonSerializerOptions);
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var iv = RandomNumberGenerator.GetBytes(12);
        var cipherText = new byte[payloadBytes.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(aesKey, tag.Length))
        {
            aes.Encrypt(iv, payloadBytes, cipherText, tag);
        }

        var encryptedAesKey = serverPublicKey.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
        var signaturePayload = BuildSignaturePayload(sensorId, encryptedAesKey, iv, cipherText, tag);
        var signature = sensorPrivateKey.SignData(signaturePayload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return new SecureEnvelopeDto
        {
            SensorId = sensorId,
            EncryptedAesKey = Convert.ToBase64String(encryptedAesKey),
            Iv = Convert.ToBase64String(iv),
            CipherText = Convert.ToBase64String(cipherText),
            Tag = Convert.ToBase64String(tag),
            Signature = Convert.ToBase64String(signature)
        };
    }

    public static byte[] DecryptPayload(SecureEnvelopeDto envelope, RSA serverPrivateKey)
    {
        var encryptedAesKey = Convert.FromBase64String(envelope.EncryptedAesKey);
        var iv = Convert.FromBase64String(envelope.Iv);
        var cipherText = Convert.FromBase64String(envelope.CipherText);
        var tag = Convert.FromBase64String(envelope.Tag);
        var aesKey = serverPrivateKey.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);
        var plainText = new byte[cipherText.Length];

        using (var aes = new AesGcm(aesKey, tag.Length))
        {
            aes.Decrypt(iv, cipherText, tag, plainText);
        }

        return plainText;
    }

    public static bool VerifySignature(SecureEnvelopeDto envelope, RSA sensorPublicKey)
    {
        var encryptedAesKey = Convert.FromBase64String(envelope.EncryptedAesKey);
        var iv = Convert.FromBase64String(envelope.Iv);
        var cipherText = Convert.FromBase64String(envelope.CipherText);
        var tag = Convert.FromBase64String(envelope.Tag);
        var signaturePayload = BuildSignaturePayload(envelope.SensorId, encryptedAesKey, iv, cipherText, tag);
        var signature = Convert.FromBase64String(envelope.Signature);

        return sensorPublicKey.VerifyData(signaturePayload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    public static string ComputePayloadHash(byte[] payload)
    {
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash);
    }

    private static byte[] BuildSignaturePayload(string sensorId, byte[] encryptedAesKey, byte[] iv, byte[] cipherText, byte[] tag)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(sensorId);
        writer.Write(encryptedAesKey.Length);
        writer.Write(encryptedAesKey);
        writer.Write(iv.Length);
        writer.Write(iv);
        writer.Write(cipherText.Length);
        writer.Write(cipherText);
        writer.Write(tag.Length);
        writer.Write(tag);
        writer.Flush();
        return stream.ToArray();
    }
}
