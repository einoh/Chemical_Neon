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
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return Convert.ToHexString(hash);
            }
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
        /// </summary>
        public static bool IsTimestampValid(string timestamp, int toleranceSeconds = 300)
        {
            if (!long.TryParse(timestamp, out var requestTime))
                return false;

            var requestDateTime = UnixTimeStampToDateTime(requestTime);
            var timeDifference = Math.Abs((DateTime.UtcNow - requestDateTime).TotalSeconds);

            return timeDifference <= toleranceSeconds;
        }

        private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToUniversalTime();
            return dateTime;
        }
    }
}
