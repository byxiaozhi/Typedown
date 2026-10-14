using System;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;

namespace Typedown.Core.Utilities
{
    /// <summary>
    /// Stores secrets (e.g. the HedgeDoc password) in settings encrypted for the current Windows user (DPAPI) instead
    /// of plain text. Values that could not be protected are stored as-is, so a broken DPAPI never locks the user out.
    /// </summary>
    public static class Secret
    {
        private const string Prefix = "dp1:";
        private const string Descriptor = "LOCAL=user";

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            try
            {
                var provider = new DataProtectionProvider(Descriptor);
                var buffer = CryptographicBuffer.ConvertStringToBinary(plain, BinaryStringEncoding.Utf8);
                var protectedBuffer = provider.ProtectAsync(buffer).AsTask().GetAwaiter().GetResult();
                return Prefix + CryptographicBuffer.EncodeToBase64String(protectedBuffer);
            }
            catch (Exception ex)
            {
                Log.Debug($"Secret.Protect failed, storing unprotected: {ex.Message}");
                return plain;
            }
        }

        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
            if (!stored.StartsWith(Prefix, StringComparison.Ordinal)) return stored;
            try
            {
                var provider = new DataProtectionProvider();
                var buffer = CryptographicBuffer.DecodeFromBase64String(stored.Substring(Prefix.Length));
                var plainBuffer = provider.UnprotectAsync(buffer).AsTask().GetAwaiter().GetResult();
                return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, plainBuffer);
            }
            catch (Exception ex)
            {
                Log.Debug($"Secret.Unprotect failed: {ex.Message}");
                return "";
            }
        }
    }
}
