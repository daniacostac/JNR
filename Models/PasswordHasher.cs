// File: PasswordHasher.cs (or Helpers/PasswordHasher.cs)
using System;
using System.Security.Cryptography;
using System.Text;

namespace JNR.Helpers // Or your project's root namespace
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16; // 128 bit
        private const int HashSize = 32; // 256 bit
        private const int Iterations = 50000; // Adjust as needed for performance/security balance

        public static string HashPassword(string password)
        {
            // Generate a salt
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Hash the password
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(HashSize);

                // Combine salt and hash
                byte[] hashBytes = new byte[SaltSize + HashSize];
                Array.Copy(salt, 0, hashBytes, 0, SaltSize);
                Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

                // Convert to base64 for storage
                return Convert.ToBase64String(hashBytes);
            }
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                // Convert base64 string back to bytes
                byte[] hashBytes = Convert.FromBase64String(hashedPassword);

                // Extract salt
                byte[] salt = new byte[SaltSize];
                Array.Copy(hashBytes, 0, salt, 0, SaltSize);

                // Extract original hash
                byte[] storedHash = new byte[HashSize];
                Array.Copy(hashBytes, SaltSize, storedHash, 0, HashSize);

                // Hash the input password with the stored salt
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    byte[] testHash = pbkdf2.GetBytes(HashSize);

                    // Compare the hashes
                    for (int i = 0; i < HashSize; i++)
                    {
                        if (testHash[i] != storedHash[i])
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
            catch
            {
                // Handle cases where hashedPassword is not in the expected format
                return false;
            }
        }
    }
}