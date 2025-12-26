# Security Remediation Code Examples

This file contains secure code implementations for the critical vulnerabilities found.

## 1. HTTPS Implementation

```csharp
// Startup.cs / Program.cs
services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 443;
});

app.UseHttpsRedirection();
```

```javascript
// ? FIXED Frontend
const API_URL = "https://your-domain.com/api";
```

---

## 2. Secure Session Management (Backend)

```csharp
// Models/SessionData.cs
public class SessionData
{
    public string Token { get; set; }
    public string MachineId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

// Services/SessionService.cs
public class SessionService
{
    private readonly IMemoryCache _cache;
    
    public SessionService(IMemoryCache cache)
    {
        _cache = cache;
    }
    
    public string CreateSession(string machineId)
    {
        using var rng = new RNGCryptoServiceProvider();
        var tokenData = new byte[32];
        rng.GetBytes(tokenData);
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
    
    public SessionData ValidateSession(string token)
    {
        if (_cache.TryGetValue(token, out SessionData session))
        {
            if (session.ExpiresAt > DateTime.UtcNow)
                return session;
            
            _cache.Remove(token);
        }
        return null;
    }
}

// Controller
[Route("api/[controller]")]
[ApiController]
public class VendingController : ControllerBase
{
    private readonly SessionService _sessionService;
    
    [HttpPost("session/create")]
    public IActionResult CreateSession([FromBody] SessionCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MachineId))
            return BadRequest("Invalid machine ID");
            
        var token = _sessionService.CreateSession(req.MachineId);
        return Ok(new { sessionToken = token });
    }
    
    [HttpGet("status/{machineId}")]
    public async Task<IActionResult> GetStatus(string machineId, [FromHeader(Name = "X-Session-Token")] string sessionToken)
    {
        var session = _sessionService.ValidateSession(sessionToken);
        if (session == null)
            return Unauthorized();
            
        if (session.MachineId != machineId)
            return Forbid();
        
        // ... rest of implementation
    }
}
```

---

## 3. Secure Session ID Generation (Frontend)

```javascript
// ? FIXED: Cryptographically secure session ID
function generateSecureSessionId() {
    const array = new Uint8Array(32);
    crypto.getRandomValues(array);
    return Array.from(array, byte => byte.toString(16).padStart(2, '0')).join('');
}

// For server-side session tokens:
let sessionToken = sessionStorage.getItem('vendo_session_token');
if (!sessionToken) {
    // Request token from server
    const response = await fetch(`${API_URL}/vending/session/create`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ machineId: MACHINE_ID })
    });
    const data = await response.json();
    sessionToken = data.sessionToken;
    sessionStorage.setItem('vendo_session_token', sessionToken);
}
```

---

## 4. CSRF Protection (Backend)

```csharp
// Program.cs
services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.FormFieldName = "_csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

app.UseAntiforgery();

// Controller
[HttpPost("lock")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> LockMachine([FromBody] LockRequest req)
{
    // Implementation
}

[HttpPost("buy")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> BuyVoucher([FromBody] BuyRequest req)
{
    // Implementation
}
```

```javascript
// ? Frontend: Get and send CSRF token
async function getCsrfToken() {
    const response = await fetch(`${API_URL}/csrf-token`);
    const data = await response.json();
    return data.csrfToken;
}

const csrfToken = await getCsrfToken();

fetch(`${API_URL}/vending/lock`, {
    method: 'POST',
    headers: { 
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': csrfToken
    },
    body: JSON.stringify({ machineId: MACHINE_ID, sessionId: sessionId })
});
```

---

## 5. API Authentication

```csharp
// Middleware/ApiKeyAuthenticationMiddleware.cs
public class ApiKeyAuthenticationMiddleware
{
    private const string API_KEY_HEADER = "X-API-Key";
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    
    public ApiKeyAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(API_KEY_HEADER, out var apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Missing API key" });
            return;
        }
        
        var validApiKey = _configuration["ApiKeys:VendingApi"];
        if (apiKey.ToString() != validApiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid API key" });
            return;
        }
        
        await _next(context);
    }
}

// Program.cs
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
```

```javascript
// ? Frontend
const API_KEY = "your-secure-api-key-here"; // Should come from secure source

const headers = {
    'Content-Type': 'application/json',
    'X-API-Key': API_KEY
};

fetch(`${API_URL}/vending/status/${MACHINE_ID}`, { headers });
```

---

## 6. Input Validation

```csharp
// ? Validate all inputs
[HttpGet("status/{machineId}")]
public async Task<IActionResult> GetStatus(string machineId, [FromHeader(Name = "X-Session-Token")] string sessionToken)
{
    // Validate format
    if (string.IsNullOrWhiteSpace(machineId) || !Regex.IsMatch(machineId, @"^SITE_\d{3}$"))
        return BadRequest("Invalid machine ID format");
        
    if (string.IsNullOrWhiteSpace(sessionToken) || sessionToken.Length > 100)
        return BadRequest("Invalid session token");
    
    using var conn = new MySqlConnection(_connectionString);
    await conn.OpenAsync();

    using var cmd = new MySqlCommand(
        "SELECT LockedBySessionId, LockExpiration, CurrentCredit FROM VendingMachines WHERE MachineIdentifier = @Id",
        conn);
    cmd.Parameters.AddWithValue("@Id", machineId);
    cmd.CommandTimeout = 30;

    // ... rest
}

[HttpPost("buy")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> BuyVoucher([FromBody] BuyRequest req)
{
    if (string.IsNullOrWhiteSpace(req.MachineId) || !Regex.IsMatch(req.MachineId, @"^SITE_\d{3}$"))
        return BadRequest("Invalid machine ID");
        
    if (req.DurationMinutes <= 0 || req.DurationMinutes > 10080) // Max 7 days
        return BadRequest("Invalid duration");
    
    // ... rest
}
```

---

## 7. Rate Limiting

```csharp
// Program.cs
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

---

## 8. Security Headers

```csharp
// Middleware/SecurityHeadersMiddleware.cs
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        
        await _next(context);
    }
}

// Program.cs
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
```

---

## 9. Secure Frontend Implementation

```javascript
// ? COMPLETE SECURE IMPLEMENTATION
const API_URL = "https://your-domain.com/api";
const API_KEY = "your-api-key-from-config"; // Should be injected by server

let sessionToken = null;
let machineId = null;

// Initialize
async function init() {
    machineId = document.body.getAttribute('data-machine-id');
    if (!machineId) {
        console.error('Machine ID not configured');
        return;
    }
    
    sessionToken = sessionStorage.getItem('vendo_session_token');
    if (!sessionToken) {
        await createNewSession();
    }
}

async function createNewSession() {
    try {
        const response = await fetch(`${API_URL}/vending/session/create`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-API-Key': API_KEY
            },
            body: JSON.stringify({ machineId })
        });
        
        if (!response.ok) throw new Error('Failed to create session');
        
        const data = await response.json();
        sessionToken = data.sessionToken;
        sessionStorage.setItem('vendo_session_token', sessionToken);
    } catch (e) {
        console.error('Session creation failed');
    }
}

async function checkStatus() {
    if (!sessionToken) return;
    
    try {
        const response = await fetch(
            `${API_URL}/vending/status/${machineId}`,
            {
                headers: {
                    'X-Session-Token': sessionToken,
                    'X-API-Key': API_KEY
                }
            }
        );
        
        if (response.status === 401) {
            // Session expired
            sessionStorage.removeItem('vendo_session_token');
            await createNewSession();
            return;
        }
        
        if (!response.ok) throw new Error('Status check failed');
        
        const data = await response.json();
        updateUI(data);
    } catch (e) {
        console.error('Operation failed');
    }
}

// Initialize on page load
init();
checkStatus();
```

---

## 10. Secure Configuration

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning" // Don't log debug info in production
    }
  },
  "ConnectionStrings": {
    "constr": "Server=secure-db-server;Database=VendingDB;User Id=vending_app;Password=SecurePassword123;SslMode=Required;"
  },
  "ApiKeys": {
    "VendingApi": "your-secure-api-key-here"
  },
  "Security": {
    "RequireHttps": true,
    "EnableCors": false,
    "CorsOrigins": []
  }
}
```

---

## Implementation Checklist

- [ ] Enable HTTPS with valid SSL certificate
- [ ] Implement server-side session management
- [ ] Add CSRF token generation and validation
- [ ] Add API key authentication
- [ ] Add input validation on all endpoints
- [ ] Implement rate limiting
- [ ] Add security headers
- [ ] Move machine ID to server-side injection
- [ ] Use sessionStorage instead of localStorage for sensitive data
- [ ] Add comprehensive error handling
- [ ] Remove verbose logging in production
- [ ] Set up security monitoring/alerting
- [ ] Conduct penetration testing
- [ ] Review database permissions
- [ ] Implement audit logging for financial transactions

