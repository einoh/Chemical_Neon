using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Chemical_Neon.Services
{
    public class SessionData
    {
        public required string Token { get; set; }
        public required string MachineId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class SessionService(IMemoryCache cache)
    {
        private readonly IMemoryCache _cache = cache;

        public string CreateSession(string machineId)
        {
            var tokenData = new byte[32];
            RandomNumberGenerator.Fill(tokenData);
            var token = Convert.ToBase64String(tokenData);

            var session = new SessionData
            {
                Token = token,
                MachineId = machineId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _cache.Set(token, session, TimeSpan.FromHours(1));
            return token;
        }

        public SessionData? ValidateSession(string token)
        {
            if (_cache.TryGetValue(token, out var sessionObj) && sessionObj is SessionData session)
            {
                if (session.ExpiresAt > DateTime.UtcNow)
                    return session;

                _cache.Remove(token);
            }
            return null;
        }
    }
}
