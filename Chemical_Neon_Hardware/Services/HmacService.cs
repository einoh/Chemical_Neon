using System.Security.Cryptography;
using System.Text;

namespace Chemical_Neon_Hardware.Services
{
    public class HmacService
    {
        /// <summary>
        /// Computes HMAC-SHA256 signature for the given payload and timestamp
        /// </summary>
        public static string ComputeSignature(string payload, string timestamp, string secretKey)
        {
            var message = $"{payload}:{timestamp}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            // Convert to lowercase hex to match Arduino's sprintf("%02x")
            return Convert.ToHexString(hash).ToLower();
        }

        /// <summary>
        /// Verifies HMAC-SHA256 signature and returns computed signature for debugging
        /// </summary>
        public static (bool isValid, string computedSignature) VerifySignatureWithDebug(string payload, string timestamp, string secretKey, string receivedSignature)
        {
            var computedSignature = ComputeSignature(payload, timestamp, secretKey);
            var isValid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(receivedSignature)
            );
            return (isValid, computedSignature);
        }

        /// <summary>
        /// Verifies HMAC-SHA256 signature
        /// </summary>
        public static bool VerifySignature(string payload, string timestamp, string secretKey, string receivedSignature)
        {
            var computedSignature = ComputeSignature(payload, timestamp, secretKey);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(receivedSignature)
            );
        }

        /// <summary>
        /// Validates if timestamp is within acceptable tolerance (prevents replay attacks)
        /// Handles both Unix epoch timestamps and device uptime (seconds since startup)
        /// </summary>
        public static bool IsTimestampValid(string timestamp, int toleranceSeconds = 300)
        {
            if (!long.TryParse(timestamp, out var requestTime))
                return false;

            // Strategy: If timestamp is less than ~100 years in seconds, treat it as device uptime
            // 100 years ? 3,155,760,000 seconds (reasonable cutoff)
            // If timestamp is larger, assume it's Unix epoch seconds
            const long UPTIME_CUTOFF = 3155760000;

            if (requestTime < UPTIME_CUTOFF)
            {
                // Treat as device uptime in seconds
                // Accept any positive uptime value (Arduino just booted will have small values like 238 seconds)
                return requestTime >= 0;
            }
            else
            {
                // Treat as Unix epoch timestamp
                var requestDateTime = UnixTimeStampToDateTime(requestTime);
                var timeDifference = Math.Abs((DateTime.UtcNow - requestDateTime).TotalSeconds);
                return timeDifference <= toleranceSeconds;
            }
        }

        private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToUniversalTime();
            return dateTime;
        }
    }
}
