using Chemical_Neon.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace Chemical_Neon.Controllers
{
    public class SessionCreateRequest { public required string MachineId { get; set; } }
    public class LockRequest { public required string MachineId { get; set; } public required string SessionId { get; set; } }
    public class CoinPayload { public required string MachineId { get; set; } public required string ApiKey { get; set; } public int PulseCount { get; set; } }
    public class BuyRequest { public required string MachineId { get; set; } public required string SessionId { get; set; } public int DurationMinutes { get; set; } }

    [Route("api/[controller]")]
    [ApiController]
    public class VendingController(IConfiguration configuration, FileErrorLoggerService errorLogger, SessionService sessionService) : ControllerBase
    {
        private readonly string? _connectionString = configuration.GetConnectionString("constr");
        private readonly FileErrorLoggerService _errorLogger = errorLogger;
        private readonly SessionService _sessionService = sessionService;

        // 1. CLIENT: CREATE NEW SESSION REQUEST
        [HttpPost("session/create")]
        public IActionResult CreateSession([FromBody] SessionCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.MachineId))
                return BadRequest("Invalid machine ID");

            var token = _sessionService.CreateSession(req.MachineId);
            return Ok(new { sessionToken = token });
        }

        // 2. CLIENT: CHECK STATUS / POLL BALANCE
        [HttpGet("status/{machineId}")]
        public async Task<IActionResult> GetStatus(string machineId, [FromHeader(Name = "X-Session-Token")] string sessionToken)
        {
            var session = _sessionService.ValidateSession(sessionToken);
            if (session == null)
                return Unauthorized("Invalid or expired session");

            if (session.MachineId != machineId)
                return Forbid("Session is for different machine");

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
                bool isMySession = lockedBy == sessionToken;

                // If lock expired, we consider it free
                if (lockedBy != null && expiry <= DateTime.Now) isLocked = false;

                return Ok(new
                {
                    IsLocked = isLocked,
                    LockedByMe = isMySession && isLocked,
                    CurrentCredit = (isLocked && isMySession) ? credit : 0,
                    LockExpiration = (isLocked && isMySession) ? expiry : (DateTime?)null
                });
            }

            _errorLogger.LogError($"Machine not found : {machineId}, from {sessionToken}");
            return NotFound("Machine not found");
        }

        // 3. CLIENT: ATTEMPT TO LOCK MACHINE FOR SESSION (THE "INSERT COIN" BUTTON)
        [HttpPost("lock")]
        public async Task<IActionResult> LockMachine([FromBody] LockRequest req)
        {
            var session = _sessionService.ValidateSession(req.SessionId);
            if (session == null)
                return Unauthorized("Invalid or expired session");

            if (session.MachineId != req.MachineId)
                return Forbid("Session is for different machine");

            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

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

        // 4. CLIENT: BUY VOUCHER WITH SECURED SESSION
        [HttpPost("buy")]
        public async Task<IActionResult> BuyVoucher([FromBody] BuyRequest req)
        {
            var session = _sessionService.ValidateSession(req.SessionId);
            if (session == null)
                return Unauthorized("Invalid or expired session");

            if (session.MachineId != req.MachineId)
                return Forbid("Session is for different machine");

            if (req.DurationMinutes <= 0 || req.DurationMinutes > 10080)
                return BadRequest("Invalid duration (1 minute to 7 days)");

            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var checkCmd = new MySqlCommand("SELECT CurrentCredit, LockedBySessionId FROM VendingMachines WHERE MachineIdentifier = @Id AND LockExpiration > NOW()", conn);
            checkCmd.Parameters.AddWithValue("@Id", req.MachineId);

            using var reader = await checkCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return BadRequest("Machine not locked or lock expired");

            var currentCredit = reader.GetDecimal(0);
            var lockedBy = reader.IsDBNull(1) ? null : reader.GetString(1);

            if (lockedBy != req.SessionId)
                return Forbid("This session does not own the lock");

            // Generate voucher code
            var voucherCode = GenerateVoucherCode();

            // Reset credit after purchase
            await reader.CloseAsync();
            using var updateCmd = new MySqlCommand(@"
            UPDATE VendingMachines 
            SET CurrentCredit = 0,
                LockedBySessionId = NULL,
                LockExpiration = NOW()
            WHERE MachineIdentifier = @Id", conn);

            updateCmd.Parameters.AddWithValue("@Id", req.MachineId);
            await updateCmd.ExecuteNonQueryAsync();

            _errorLogger.LogError($"Voucher created: {voucherCode} for {req.MachineId} ({req.DurationMinutes} min)");
            return Ok(new { code = voucherCode, durationMinutes = req.DurationMinutes });
        }

        // Helper method to generate voucher code
        private string GenerateVoucherCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var result = new System.Text.StringBuilder(12);
            for (int i = 0; i < 12; i++)
                result.Append(chars[random.Next(chars.Length)]);
            return result.ToString();
        }

        // 5. HARDWARE: RECIEVING COIN INSERT FROM ARDUINO
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

            _errorLogger.LogError($"Received {payload.PulseCount} pulses for {payload.MachineId}. Added {valueToAdd}");
            Console.WriteLine($"Received {payload.PulseCount} pulses for {payload.MachineId}. Added {valueToAdd}");
            return Ok();
        }
    }
}
