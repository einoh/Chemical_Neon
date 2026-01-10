using Chemical_Neon_Vending.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace Chemical_Neon_Vending.Controllers
{
    public class SessionCreateRequest { public required string MachineId { get; set; } }
    public class LockRequest { public required string MachineId { get; set; } public required string SessionId { get; set; } }
    public class BuyRequest { public required string MachineId { get; set; } public required string SessionId { get; set; } public int DurationMinutes { get; set; } }

    [Route("api/[controller]")]
    [ApiController]
    public class VendingController(IConfiguration configuration, FileErrorLoggerService errorLogger, SessionService sessionService) : ControllerBase
    {
        private readonly string? _connectionString = configuration.GetConnectionString("constr");
        private readonly FileErrorLoggerService _error_logger = errorLogger;
        private readonly SessionService _session_service = sessionService;

        // 1. CLIENT: CREATE NEW SESSION REQUEST
        [HttpPost("session/create")]
        public IActionResult CreateSession([FromBody] SessionCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.MachineId))
                return BadRequest("Invalid machine ID");

            var token = _session_service.CreateSession(req.MachineId);
            _error_logger.LogError($"Session created for {req.MachineId}. Token: {token[..8]}...");
            return Ok(new { sessionToken = token });
        }

        // 2. CLIENT: CHECK STATUS / POLL BALANCE
        [HttpGet("status/{machineId}")]
        public async Task<IActionResult> GetStatus(string machineId)
        {
            // Try to read session token from header first, then from query string as fallback
            var sessionToken = string.Empty;
            if (Request.Headers.TryGetValue("X-Session-Token", out var headerVals))
            {
                sessionToken = headerVals.ToString();
            }
            else if (Request.Query.TryGetValue("sessionToken", out var queryVals))
            {
                sessionToken = queryVals.ToString();
            }

            SessionData? session = null;
            if (!string.IsNullOrEmpty(sessionToken))
            {
                session = _session_service.ValidateSession(sessionToken);
                if (session == null)
                {
                    _error_logger.LogError($"Session validation failed for token: {sessionToken[..8]}...");
                }
            }

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
                bool isMySession = session != null && lockedBy == sessionToken;

                // If lock expired, we consider it free
                if (lockedBy != null && expiry <= DateTime.Now) isLocked = false;

                // If session is present but machine belongs to different machine, forbid details
                if (session != null && session.MachineId != machineId)
                    return Forbid("Session is for different machine");

                return Ok(new
                {
                    IsLocked = isLocked,
                    LockedByMe = isMySession && isLocked,
                    CurrentCredit = (isLocked && isMySession) ? credit : 0,
                    LockExpiration = (isLocked && isMySession) ? expiry : (DateTime?)null
                });
            }

            _error_logger.LogError($"Machine not found : {machineId}, from {sessionToken}");
            return NotFound("Machine not found");
        }

        // 3. CLIENT: ATTEMPT TO LOCK MACHINE FOR SESSION (THE "INSERT COIN" BUTTON)
        [HttpPost("lock")]
        public async Task<IActionResult> LockMachine([FromBody] LockRequest req)
        {
            _error_logger.LogError($"Lock request for machine {req.MachineId} with session {req.SessionId[..8]}...");
            
            var session = _session_service.ValidateSession(req.SessionId);
            if (session == null)
            {
                _error_logger.LogError($"Session validation failed for lock request. Token: {req.SessionId[..8]}...");
                return Unauthorized("Invalid or expired session");
            }

            if (session.MachineId != req.MachineId)
            {
                _error_logger.LogError($"Machine mismatch: session for {session.MachineId}, request for {req.MachineId}");
                return Forbid("Session is for different machine");
            }

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

            if (rowsAffected > 0)
            {
                _error_logger.LogError($"Machine {req.MachineId} locked successfully");
                return Ok(new { message = "Machine locked for you. Insert coins now." });
            }

            _error_logger.LogError($"Failed to lock machine {req.MachineId} - already locked");
            return BadRequest(new { message = "Machine is busy. Please wait." });
        }

        // 4. CLIENT: BUY VOUCHER WITH SECURED SESSION
        [HttpPost("buy")]
        public async Task<IActionResult> BuyVoucher([FromBody] BuyRequest req)
        {
            var session = _session_service.ValidateSession(req.SessionId);
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

            // Fetch voucher code from database
            await reader.CloseAsync();
            
            var voucherCode = await GetAvailableVoucherCode(conn, req.MachineId, req.DurationMinutes);
            
            if (string.IsNullOrEmpty(voucherCode))
            {
                _error_logger.LogError($"No available vouchers for machine {req.MachineId} with duration {req.DurationMinutes} minutes");
                return BadRequest("No vouchers available for this duration");
            }

            // Mark voucher as used
            using var updateVoucherCmd = new MySqlCommand("UPDATE vouchers SET IsUsed = NOW() WHERE Code = @Code", conn);
            updateVoucherCmd.Parameters.AddWithValue("@Code", voucherCode);
            await updateVoucherCmd.ExecuteNonQueryAsync();

            // Reset credit after purchase
            using var updateCmd = new MySqlCommand(@"
            UPDATE VendingMachines 
            SET CurrentCredit = 0,
                LockedBySessionId = NULL,
                LockExpiration = NOW()
            WHERE MachineIdentifier = @Id", conn);

            updateCmd.Parameters.AddWithValue("@Id", req.MachineId);
            await updateCmd.ExecuteNonQueryAsync();

            _error_logger.LogError($"Voucher created: {voucherCode} for {req.MachineId} ({req.DurationMinutes} min)");
            return Ok(new { code = voucherCode, durationMinutes = req.DurationMinutes });
        }

        // Helper method to fetch available voucher code from database
        private static async Task<string?> GetAvailableVoucherCode(MySqlConnection conn, string machineId, int durationMinutes)
        {
            using var cmd = new MySqlCommand("SELECT vouchers.`Code` FROM vouchers WHERE vouchers.MachineIdentifier = @MachineId AND vouchers.DurationMinutes = @Duration AND vouchers.IsUsed IS NULL LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@MachineId", machineId);
            cmd.Parameters.AddWithValue("@Duration", durationMinutes);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader.GetString(0);
            }

            return null;
        }
    }
}
