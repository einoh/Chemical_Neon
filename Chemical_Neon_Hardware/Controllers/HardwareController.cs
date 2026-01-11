using Chemical_Neon_Hardware.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace Chemical_Neon_Hardware.Controllers
{
    public class CoinPayload 
    { 
        public required string MachineId { get; set; }
        public required int PulseCount { get; set; }
        public required string Timestamp { get; set; }
        public required string Signature { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class HardwareController(IConfiguration configuration, FileErrorLoggerService errorLogger) : ControllerBase
    {
        private readonly string? _connectionString = configuration.GetConnectionString("constr");
        private readonly FileErrorLoggerService _error_logger = errorLogger;
        private readonly string? _hmacSecretKey = configuration["HmacSecretKey"];

        [HttpPost("coin")]
        public async Task<IActionResult> ReceiveCoin([FromBody] CoinPayload payload)
        {
            // Validate HMAC secret key is configured
            if (string.IsNullOrWhiteSpace(_hmacSecretKey))
            {
                _error_logger.LogError("HMAC secret key not configured");
                return StatusCode(500, new { error = "Server configuration error" });
            }

            // Log incoming request for debugging - safely handle short signatures
            var signaturePreview = string.IsNullOrEmpty(payload.Signature)
                ? "[empty]"
                : payload.Signature.Length > 16
                    ? $"{payload.Signature[..16]}..."
                    : payload.Signature;

            _error_logger.LogError($"[ReceiveCoin] Received request - Machine: {payload.MachineId}, Pulses: {payload.PulseCount}, Timestamp: {payload.Timestamp}, Signature: {signaturePreview}");

            // Validate timestamp to prevent replay attacks
            if (!HmacService.IsTimestampValid(payload.Timestamp))
            {
                _error_logger.LogError($"❌ Invalid timestamp for {payload.MachineId}: {payload.Timestamp} (Difference from now is too large - possible time sync issue)");
                return Unauthorized(new { error = "Timestamp expired or invalid. Check Arduino time synchronization." });
            }

            _error_logger.LogError($"✓ Timestamp valid for {payload.MachineId}: {payload.Timestamp}");

            // Compute expected signature
            var payloadString = $"{payload.MachineId}:{payload.PulseCount}";
            var fullMessage = $"{payloadString}:{payload.Timestamp}";
            
            // Log the exact message and signature for debugging
            _error_logger.LogError($"   Computing HMAC with message: '{fullMessage}'");
            _error_logger.LogError($"   Received signature: {payload.Signature}");
            
            // Verify HMAC signature with debugging
            var (signatureValid, computedSig) = HmacService.VerifySignatureWithDebug(payloadString, payload.Timestamp, _hmacSecretKey, payload.Signature);
            _error_logger.LogError($"   Computed signature: {computedSig}");
            _error_logger.LogError($"   Match: {signatureValid}");
            
            if (!signatureValid)
            {
                _error_logger.LogError($"❌ Invalid HMAC signature for machine {payload.MachineId}. Message: '{fullMessage}'");
                _error_logger.LogError($"      Expected: {computedSig}");
                _error_logger.LogError($"      Received: {payload.Signature}");
                return Unauthorized(new { error = "Invalid signature" });
            }

            _error_logger.LogError($"✓ Signature valid for {payload.MachineId}");

            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            // Verify machine exists and is locked
            var validateCmd = new MySqlCommand(
                "SELECT 1 FROM VendingMachines WHERE MachineIdentifier = @Id AND LockExpiration > NOW() AND LockedBySessionId IS NOT NULL",
                conn);
            validateCmd.Parameters.AddWithValue("@Id", payload.MachineId);

            var machineExists = await validateCmd.ExecuteScalarAsync();
            if (machineExists == null)
            {
                _error_logger.LogError($"❌ Machine not found or not locked: {payload.MachineId}");
                return BadRequest(new { error = "Machine not found or not locked" });
            }

            _error_logger.LogError($"✓ Machine {payload.MachineId} is locked and ready");

            // Get rate per pulse for this machine
            var getRateCmd = new MySqlCommand(
                "SELECT RatePerPulse FROM VendingMachines WHERE MachineIdentifier = @Id",
                conn);
            getRateCmd.Parameters.AddWithValue("@Id", payload.MachineId);

            var rateObj = await getRateCmd.ExecuteScalarAsync();
            if (rateObj == null)
                return BadRequest(new { error = "Machine not found" });

            decimal rate = Convert.ToDecimal(rateObj);
            decimal valueToAdd = payload.PulseCount * rate;

            // Update credit and extend lock expiration
            var updateCmd = new MySqlCommand(@"
            UPDATE VendingMachines 
            SET CurrentCredit = CurrentCredit + @Val,
                LockExpiration = DATE_ADD(NOW(), INTERVAL 60 SECOND)
            WHERE MachineIdentifier = @Id", conn);

            updateCmd.Parameters.AddWithValue("@Val", valueToAdd);
            updateCmd.Parameters.AddWithValue("@Id", payload.MachineId);

            await updateCmd.ExecuteNonQueryAsync();

            _error_logger.LogError($"✅ SUCCESS: {payload.PulseCount} pulses for {payload.MachineId}. Added ₱{valueToAdd}. Signature verified. New balance credited.");
            return Ok(new { success = true, creditAdded = valueToAdd });
        }
    }
}
