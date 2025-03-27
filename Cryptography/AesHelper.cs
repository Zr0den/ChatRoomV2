using System.Security.Cryptography;
using System.Text;

namespace Cryptography;

public class AesHelper
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"); // 32 bytes for AES-256

    public static byte[] Encrypt(string plaintext)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Key;
            aes.GenerateIV(); // New IV for each message

            using (MemoryStream ms = new MemoryStream())
            {
                // Write IV first
                ms.Write(aes.IV, 0, aes.IV.Length);

                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                using (StreamWriter writer = new StreamWriter(cs))
                {
                    writer.Write(plaintext);
                }

                return ms.ToArray(); // Returns IV + EncryptedData
            }
        }
    }

    public static string Decrypt(byte[] encryptedData)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Key;

            // Extract IV (first 16 bytes)
            byte[] iv = new byte[16];
            Array.Copy(encryptedData, 0, iv, 0, 16);
            aes.IV = iv;

            using (MemoryStream ms = new MemoryStream(encryptedData, 16, encryptedData.Length - 16))
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (StreamReader reader = new StreamReader(cs))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
