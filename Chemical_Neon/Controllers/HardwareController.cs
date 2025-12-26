using Chemical_Neon.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace Chemical_Neon.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HardwareController(IConfiguration configuration, FileErrorLoggerService errorLogger) : ControllerBase
    {
        private const decimal PRICE_PER_PULSE = 1.00m;
        private readonly string? _connectionString = configuration.GetConnectionString("constr");
        private readonly FileErrorLoggerService _errorLogger = errorLogger;

        [HttpPost("coin")]
        public async Task<IActionResult> ReceiveCoin([FromBody] Models.CoinPayload payload)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            // 1. Fetch Machine State
            // We fetch the lock status and API key to validate
            string sqlCheck = @"SELECT api_key, locked_by_session_id, lock_expiration, current_balance FROM vending_machines WHERE machine_id = @mid";

            using var cmd = new MySqlCommand(sqlCheck, conn);
            cmd.Parameters.AddWithValue("@mid", payload.MachineId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.Read())
            {
                _errorLogger.LogError("Invalid Machine ID");
                return Unauthorized("Invalid Machine ID");
            }

            string? storedApiKey = reader["api_key"].ToString();
            string? lockedBy = reader["locked_by_session_id"] as string;
            DateTime? expiration = reader["lock_expiration"] as DateTime?;
            decimal currentBalance = (decimal)reader["current_balance"];

            if (storedApiKey != payload.ApiKey)
            {
                _errorLogger.LogError("Invalid API Key");
                return Unauthorized("Invalid API Key");
            }

            reader.Close(); // Close reader before executing update

            // 2. The Critical Logic
            // Check if machine is currently locked by a valid session
            if (!string.IsNullOrEmpty(lockedBy) && expiration > DateTime.UtcNow)
            {
                // Valid lock: Credit balance & Extend lock
                decimal amountToAdd = payload.PulseCount * PRICE_PER_PULSE;

                string sqlUpdate = @"UPDATE vending_machines SET current_balance = current_balance + @amount, lock_expiration = DATEADD(second, 60, GETUTCDATE()) WHERE machine_id = @mid";

                using (var updateCmd = new MySqlCommand(sqlUpdate, conn))
                {
                    updateCmd.Parameters.AddWithValue("@amount", amountToAdd);
                    updateCmd.Parameters.AddWithValue("@mid", payload.MachineId);
                    await updateCmd.ExecuteNonQueryAsync();
                }

                _errorLogger.LogError($"Coin Credited @ {payload.MachineId} : New Balance {currentBalance + amountToAdd}");
                return Ok(new { status = "credited", newBalance = currentBalance + amountToAdd });
            }
            else
            {
                // Machine idle/expired: Ignore coin
                _errorLogger.LogError($"No Active Session @ {payload.MachineId} : Coin Ignored");
                return Ok(new { status = "ignored_no_active_session" });
            }
        }
    }
}
