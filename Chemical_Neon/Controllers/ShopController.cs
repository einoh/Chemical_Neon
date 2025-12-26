using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace Chemical_Neon.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShopController(IConfiguration configuration, IHttpContextAccessor httpContext) : ControllerBase
    {
        private readonly IHttpContextAccessor _httpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
        private readonly string _connectionString = configuration?.GetConnectionString("constr") ?? throw new ArgumentNullException(nameof(configuration));
        private string GetSessionId() => _httpContext.HttpContext?.Session?.Id ?? string.Empty;

        [HttpPost("lock")]
        public async Task<IActionResult> LockMachine(string machineId)
        {
            string mySession = GetSessionId();

            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            // ATOMIC UPDATE:
            // Attempt to take the lock ONLY IF:
            // 1. Currently unlocked (NULL)
            // 2. OR Lock expired
            // 3. OR I already own the lock (re-locking)

            string sql = @"UPDATE vending_machines SET locked_by_session_id = @session, lock_expiration = DATE_ADD(UTC_TIMESTAMP(), INTERVAL 120 SECOND),
                        current_balance = CASE WHEN locked_by_session_id = @session THEN current_balance ELSE 0 END WHERE machine_id = @mid AND 
                        (locked_by_session_id IS NULL OR lock_expiration < UTC_TIMESTAMP() OR locked_by_session_id = @session);";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@session", mySession);
            cmd.Parameters.AddWithValue("@mid", machineId);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                return new JsonResult(new { success = true });
            }
            else
            {
                // Rows affected = 0 means someone else holds a valid lock
                return new JsonResult(new { success = false, message = "Machine is busy. Please wait." });
            }
        }

        // GET: /Shop/CheckBalance
        [HttpGet("check")]
        public async Task<IActionResult> CheckBalance(string machineId)
        {
            string mySession = GetSessionId();

            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"SELECT locked_by_session_id, lock_expiration, current_balance 
                           FROM vending_machines WHERE machine_id = @mid";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@mid", machineId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (reader.Read())
                {
                    var lockedBy = reader["locked_by_session_id"] as string;
                    var expiration = reader["lock_expiration"] as DateTime?;
                    var balance = (decimal)reader["current_balance"];

                    // Verify ownership
                    if (lockedBy == mySession && expiration > DateTime.UtcNow)
                    {
                        return new JsonResult(new
                        {
                            locked = true,
                            balance,
                            timeLeft = (expiration.Value - DateTime.UtcNow).TotalSeconds
                        });
                    }
                }
            }

            return new JsonResult(new { locked = false, balance = 0 });
        }

    }
}
