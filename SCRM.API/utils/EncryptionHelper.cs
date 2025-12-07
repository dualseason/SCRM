using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SCRM.API.Utils
{
    public static class EncryptionHelper
    {
        private const string DefaultKey = "k!ha#@ff";

        public static string DecryptDefault(string base64Input)
        {
            return Decrypt(base64Input, DefaultKey);
        }

        public static string Decrypt(string base64Input, string keyStr)
        {
            if (string.IsNullOrEmpty(base64Input)) return null;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(base64Input);
                byte[] keyBytes = Encoding.UTF8.GetBytes(keyStr);
                byte[] decryptedBytes = DecryptRaw(encryptedBytes, keyBytes);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Decryption error: {ex.Message}");
                return null;
            }
        }

        private static byte[] DecryptRaw(byte[] data, byte[] keyBytes)
        {
            using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
            {
                des.Key = keyBytes;
                des.Mode = CipherMode.ECB;
                des.Padding = PaddingMode.PKCS7; // Java's PKCS5Padding is compatible with PKCS7

                using (ICryptoTransform decryptor = des.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }

        public static string EncryptDefault(string input)
        {
            return Encrypt(input, DefaultKey);
        }

        public static string Encrypt(string input, string keyStr)
        {
            if (string.IsNullOrEmpty(input)) return null;

            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] keyBytes = Encoding.UTF8.GetBytes(keyStr);
            byte[] encryptedBytes = EncryptRaw(inputBytes, keyBytes);
            return Convert.ToBase64String(encryptedBytes);
        }

        private static byte[] EncryptRaw(byte[] data, byte[] keyBytes)
        {
            using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
            {
                des.Key = keyBytes;
                des.Mode = CipherMode.ECB;
                des.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform encryptor = des.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }
    }
}
