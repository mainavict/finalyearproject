using System;
using System.Security.Cryptography;
using System.Text;

namespace finalyearproject.Security;

public static class RecallWebhookVerifier
{
    /// <summary>
    /// Verifies the cryptographic signature of a webhook request from Recall.ai.
    /// </summary>
    public static bool VerifySignature(string secret, string msgId, string msgTimestamp, string rawPayload, string msgSignature)
    {
        if (string.IsNullOrEmpty(secret) || !secret.StartsWith("whsec_"))
            throw new ArgumentException("Invalid secret format. Must start with 'whsec_'.");

        // 1. Decode the base64 secret key (strip the "whsec_" prefix)
        var base64Part = secret.AsSpan(6); // Skip "whsec_"
        var key = Convert.FromBase64String(base64Part.ToString());

        // 2. Create the string to sign: "{id}.{timestamp}.{payload}"
        var toSign = $"{msgId}.{msgTimestamp}.{rawPayload}";

        // 3. Compute HMAC-SHA256
        using var hmac = new HMACSHA256(key);
        var expectedSigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign));

        // 4. The header can contain multiple signatures separated by spaces (e.g., during secret rotation)
        var passedSigs = msgSignature.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var versionedSig in passedSigs)
        {
            var parts = versionedSig.Split(',', 2);
            if (parts.Length != 2 || parts[0] != "v1")
                continue;

            var sigBytes = Convert.FromBase64String(parts[1]);

            // 5. Timing-safe comparison to prevent timing attacks
            if (CryptographicOperations.FixedTimeEquals(expectedSigBytes, sigBytes))
            {
                return true;
            }
        }

        return false;
    }
}