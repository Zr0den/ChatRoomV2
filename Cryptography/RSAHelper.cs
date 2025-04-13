using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Cryptography
{
    // RSAHelper for key generation and encryption/decryption
    public class RSAHelper
    {
        private readonly RSA _rsa;

        public RSAHelper()
        {
            _rsa = RSA.Create(2048);
        }

        public string PublicKey => Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());
        public string ExportPrivateKey() => Convert.ToBase64String(_rsa.ExportPkcs8PrivateKey());
        public void ImportPrivateKey(string base64) => _rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(base64), out _);

        public static byte[] Encrypt(byte[] data, string base64PublicKey)
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(base64PublicKey), out _);
            return rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
        }

        public byte[] DecryptRaw(byte[] cipherText)
        {
            return _rsa.Decrypt(cipherText, RSAEncryptionPadding.OaepSHA256);
        }

        public byte[] SignData(byte[] data)
        {
            return _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        public static bool VerifySignature(byte[] data, byte[] signature, string base64PublicKey)
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(base64PublicKey), out _);
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }
}
