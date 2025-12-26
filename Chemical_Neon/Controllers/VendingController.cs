using Chemical_Neon.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace Chemical_Neon.Controllers
{
    public class LockRequest { public required string MachineId { get; set; } public required string SessionId { get; set; } }
    public class CoinPayload { public required string MachineId { get; set; } public required string ApiKey { get; set; } public int PulseCount { get; set; } }
    public class BuyRequest { public required string MachineId { get; set; } public required string SessionId { get; set; } public int DurationMinutes { get; set; } }

    [Route("api/[controller]")]
    [ApiController]
    public class VendingController(IConfiguration configuration, FileErrorLoggerService errorLogger) : ControllerBase
    {
        private readonly string? _connectionString = configuration.GetConnectionString("constr");
        private readonly FileErrorLoggerService _errorLogger = errorLogger;

        // 1. CLIENT: Check Status / Poll Balance
        [HttpGet("status/{machineId}")]
        public async Task<IActionResult> GetStatus(string machineId, [FromQuery] string sessionId)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("SELECT LockedBySessionId, LockExpiration, CurrentCredit FROM VendingMachines WHERE MachineIdentifier = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", machineId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var lockedBy = reader.IsDBNull(0) ? null : reader.GetString(0);
                var expiry = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
                var credit = reader.GetDecimal(2);

                bool isLocked = lockedBy != null && expiry > DateTime.Now;
                bool isMySession = lockedBy == sessionId;

                // If lock expired, we consider it free
                if (lockedBy != null && expiry <= DateTime.Now) isLocked = false;

                return Ok(new
                {
                    IsLocked = isLocked,
                    LockedByMe = isLocked && isMySession,
                    CurrentCredit = (isLocked && isMySession) ? credit : 0,
                    LockExpiration = (isLocked && isMySession) ? expiry : (DateTime?)null
                });
            }

            _errorLogger.LogError($"Machine not found : {machineId}, from {sessionId}");
            return NotFound("Machine not found");
        }

        // 2. CLIENT: Attempt to Lock Machine (The "Insert Coin" Button)
        [HttpPost("lock")]
        public async Task<IActionResult> LockMachine([FromBody] LockRequest req)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            // Try to update ONLY if currently unlocked or expired
            // This is an atomic operation to prevent race conditions
            var query = @"
            UPDATE VendingMachines 
            SET LockedBySessionId = @Session, 
                LockExpiration = DATE_ADD(NOW(), INTERVAL 60 SECOND),
                CurrentCredit = 0 
            WHERE MachineIdentifier = @Id 
            AND (LockedBySessionId IS NULL OR LockExpiration < NOW())";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Session", req.SessionId);
            cmd.Parameters.AddWithValue("@Id", req.MachineId);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0) return Ok(new { message = "Machine locked for you. Insert coins now." });

            return BadRequest(new { message = "Machine is busy. Please wait." });
        }

        // 3. HARDWARE: Receive Pulse from Arduino
        [HttpPost("hardware/coin")]
        public async Task<IActionResult> ReceiveCoin([FromBody] CoinPayload payload)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            // 1. Validate API Key
            var validateCmd = new MySqlCommand("SELECT RatePerPulse FROM VendingMachines WHERE MachineIdentifier = @Id AND ApiKey = @Key", conn);
            validateCmd.Parameters.AddWithValue("@Id", payload.MachineId);
            validateCmd.Parameters.AddWithValue("@Key", payload.ApiKey);

            var rateObj = await validateCmd.ExecuteScalarAsync();
            if (rateObj == null) return Unauthorized();

            decimal rate = Convert.ToDecimal(rateObj);
            decimal valueToAdd = payload.PulseCount * rate;

            // 2. Add Credit ONLY if Locked and Not Expired
            // If machine is idle (not locked), the coin is unfortunately 'eaten' or logged as donation
            var updateCmd = new MySqlCommand(@"
            UPDATE VendingMachines 
            SET CurrentCredit = CurrentCredit + @Val,
                LockExpiration = DATE_ADD(NOW(), INTERVAL 60 SECOND) -- Extend timer on coin insert
            WHERE MachineIdentifier = @Id 
            AND LockExpiration > NOW() 
            AND LockedBySessionId IS NOT NULL", conn);

            updateCmd.Parameters.AddWithValue("@Val", valueToAdd);
            updateCmd.Parameters.AddWithValue("@Id", payload.MachineId);

            await updateCmd.ExecuteNonQueryAsync();

            _errorLogger.LogError($"Received {payload.PulseCount} pulses for {payload.MachineId}. Added {valueToAdd}");
            Console.WriteLine($"Received {payload.PulseCount} pulses for {payload.MachineId}. Added {valueToAdd}");
            return Ok();
        }
    }
}
