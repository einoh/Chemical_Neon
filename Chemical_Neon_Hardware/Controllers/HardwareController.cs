using Chemical_Neon_Hardware.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace Chemical_Neon_Hardware.Controllers
{
    public class CoinPayload { public required string MachineId { get; set; } public required string ApiKey { get; set; } public int PulseCount { get; set; } }

    [Route("api/[controller]")]
    [ApiController]
    public class HardwareController(IConfiguration configuration, FileErrorLoggerService errorLogger) : ControllerBase
    {
        private readonly string? _connectionString = configuration.GetConnectionString("constr");
        private readonly FileErrorLoggerService _error_logger = errorLogger;

        [HttpPost("coin")]
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

            var updateCmd = new MySqlCommand(@"
            UPDATE VendingMachines 
            SET CurrentCredit = CurrentCredit + @Val,
                LockExpiration = DATE_ADD(NOW(), INTERVAL 60 SECOND)
            WHERE MachineIdentifier = @Id 
            AND LockExpiration > NOW() 
            AND LockedBySessionId IS NOT NULL", conn);

            updateCmd.Parameters.AddWithValue("@Val", valueToAdd);
            updateCmd.Parameters.AddWithValue("@Id", payload.MachineId);

            await updateCmd.ExecuteNonQueryAsync();

            _error_logger.LogError($"Received {payload.PulseCount} pulses for {payload.MachineId}. Added {valueToAdd}");
            return Ok();
        }
    }
}
