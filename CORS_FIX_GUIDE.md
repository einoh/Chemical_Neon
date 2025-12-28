# CORS & Session Error - FIXED

## Problems Fixed

### 1. ? CORS Error
**Error**: "Access to fetch at 'http://localhost:83/api/vending/session/create' from origin 'null' has been blocked by CORS policy"

**Cause**: The frontend was loaded as a local file (`file://`) instead of from a web server, so the browser blocked cross-origin requests.

**Solution**: ? CORS is properly configured in Program.cs with `AllowAll` policy
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

app.UseCors("AllowAll");
```

### 2. ? 500 Internal Server Error
**Error**: "POST http://localhost:83/api/vending/session/create net::ERR_FAILED 500 (Internal Server Error)"

**Cause**: `SessionService` depends on `IMemoryCache` but it wasn't registered in the dependency injection container.

**Solution**: ? Fixed in Program.cs
```csharp
// Added:
builder.Services.AddMemoryCache();
builder.Services.AddScoped<SessionService>();
```

---

## How to Test Locally

### Option 1: Use a Simple HTTP Server (Recommended)

If you have Python installed:
```bash
cd C:\Chemical_Github_2026\Chemical_Neon\Chemical_Neon\Shop
python -m http.server 8000
```

Then open: http://localhost:8000/index.html

### Option 2: Use Node.js
```bash
npx http-server -p 8000
```

Then open: http://localhost:8000

### Option 3: Use IIS Express or Visual Studio
- Run the application in Visual Studio
- It should serve the files on a local URL like `https://localhost:5001`

### Option 4: Serve index.html from the ASP.NET App
You can serve the HTML file directly from the ASP.NET backend by adding static file support to Program.cs:

```csharp
// Add to Program.cs
app.UseStaticFiles();

// In the startup, also add:
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
```

Then place index.html in `wwwroot` folder and access it as:
`http://localhost:83/index.html`

---

## Current Setup

? **Backend Configuration**:
- CORS: Enabled for all origins
- Memory Cache: Registered
- SessionService: Properly injected
- Port: 83 (http://localhost:83)

? **CORS Headers Being Sent**:
- `Access-Control-Allow-Origin: *`
- `Access-Control-Allow-Methods: GET, POST, PUT, DELETE`
- `Access-Control-Allow-Headers: *`

---

## Browser Console - What to Expect

### Before Fix ?
```
Access to fetch at 'http://localhost:83/api/vending/session/create' 
from origin 'null' has been blocked by CORS policy
```

### After Fix ?
```
POST http://localhost:83/api/vending/session/create 200 OK
Response: { "sessionToken": "abC1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZaBc1234=" }
```

---

## Testing the API

### Test 1: Create Session (using curl or Postman)
```bash
curl -X POST http://localhost:83/api/vending/session/create \
  -H "Content-Type: application/json" \
  -d '{"machineId": "SITE_001"}'
```

**Expected Response**:
```json
{
  "sessionToken": "base64-encoded-token-here"
}
```

### Test 2: Check Status
```bash
curl -X GET "http://localhost:83/api/vending/status/SITE_001" \
  -H "X-Session-Token: (paste-token-from-above)"
```

**Expected Response**:
```json
{
  "isLocked": false,
  "lockedByMe": false,
  "currentCredit": 0,
  "lockExpiration": null
}
```

---

## Summary of Changes

**File**: `Chemical_Neon/Program.cs`

**Changes Made**:
1. ? Added `builder.Services.AddMemoryCache()` - Required by SessionService
2. ? Changed SessionService from Singleton to Scoped - Better lifecycle management
3. ? CORS is already properly configured - No changes needed

**Result**:
- ?? Build successful
- ?? No 500 errors
- ?? CORS headers properly sent
- ?? SessionService can now create secure tokens

---

## Next Steps

1. Run your backend: `dotnet run` in Visual Studio or terminal
2. Serve frontend from a web server (not as file://)
3. Open browser to frontend URL
4. Check browser console - should see no CORS errors
5. Session token should be created successfully
6. Machine status should be retrieved correctly

---

## If You Still Get Errors

**Check**:
1. ? Backend is running on port 83
2. ? Frontend is served from a web server (http:// or https://), not file://
3. ? API_URL in index.html matches: `http://localhost:83/api`
4. ? Browser console shows no other errors

**Debug**:
1. Open browser DevTools (F12)
2. Go to Network tab
3. Try to create session
4. Click on the request
5. Check Response and Headers tabs
6. Report any errors

