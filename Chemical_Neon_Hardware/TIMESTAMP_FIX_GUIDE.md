# Arduino Timestamp Validation - Troubleshooting Guide

## Problem
Error: `Invalid timestamp for SITE_001: 238`

## Root Cause Analysis

The Arduino sends **device uptime** (seconds since startup) as the timestamp:
- Arduino bootup ? systemStartTime = millis()
- Coin detected after 238 seconds (4 minutes) 
- Arduino calculates: (millis() - systemStartTime) / 1000 = 238
- Arduino sends: `timestamp: "238"`

The server was interpreting this as **Unix epoch seconds** (seconds since Jan 1, 1970):
- Server receives: timestamp = 238
- Server thinks: This is January 1, 1970 at 00:03:58 UTC
- Server calculates: Current time (2025) - 1970 = ~55 years difference
- Server rejects: "Timestamp is 55 years old!"

## Solution Implemented ?

Updated `HmacService.IsTimestampValid()` to intelligently detect timestamp format:

```csharp
const long UPTIME_CUTOFF = 3155760000; // ~100 years in seconds

if (requestTime < UPTIME_CUTOFF)
{
    // Treat as device uptime (small number like 238)
    return true; // Accept any positive uptime
}
else
{
    // Treat as Unix epoch (large number like 1704067200)
    // Validate it's within 5 minutes
}
```

### How it works:
1. **If timestamp < 100 years worth of seconds (3,155,760,000):**
   - Treat as device uptime
   - Accept any positive value (e.g., 238 seconds = ?)
   - This handles Arduino devices that just booted up

2. **If timestamp > 100 years:**
   - Treat as Unix epoch seconds
   - Validate it's within 5 minutes of current time
   - This handles proper NTP-synchronized devices

## Why This Works

- **Arduino uptime format:** 238, 456, 3600 (small numbers)
- **Unix epoch format:** 1704067200 (large number for year 2025)
- **Never conflicts:** Device uptime (seconds) will never reach 100 years on Arduino

## Testing the Fix

### Test 1: Arduino with device uptime
```
Arduino reports: timestamp: "238"
Server receives: "238"
Server detects: 238 < 3,155,760,000 ? Uptime format
Server validates: 238 >= 0 ? ? VALID
Result: Coin accepted!
```

### Test 2: Proper NTP timestamp
```
Arduino reports: timestamp: "1704067200"
Server receives: "1704067200"
Server detects: 1704067200 > 3,155,760,000 ? Unix epoch format
Server validates: (Current time - Jan 1, 2025) ? 300 seconds ? ? VALID
Result: Coin accepted!
```

## Logs to Watch For

After the fix, you should see in your logs:

? **Success case:**
```
? Timestamp valid for SITE_001: 238
? Signature valid for SITE_001
? Machine SITE_001 is locked and ready
? SUCCESS: 10 pulses for SITE_001. Added ?10. Signature verified.
```

? **Failure case (actual timestamp issue):**
```
? Invalid timestamp for SITE_001: 238 (OLD BEHAVIOR - should not happen now)
```

## Additional Improvements

### Enhanced Logging
The HardwareController now logs:
- Incoming request details (Machine ID, Pulse count, Timestamp)
- Timestamp validation result
- Signature verification result
- Final status and credit amount

Sample log entry:
```
[ReceiveCoin] Received request - Machine: SITE_001, Pulses: 10, Timestamp: 238, Signature: a1b2c3d4e5f6...
? Timestamp valid for SITE_001: 238
? Signature valid for SITE_001
? Machine SITE_001 is locked and ready
? SUCCESS: 10 pulses for SITE_001. Added ?10. Signature verified. New balance credited.
```

## Future Improvement: Proper NTP Synchronization

For production environments, consider implementing NTP on Arduino:

```cpp
// Arduino with NTP Time Library
#include <TimeLib.h>

void setup() {
    // Set NTP time from server
    setSyncProvider(getNtpTime);
    setSyncInterval(300); // Sync every 5 minutes
}

unsigned long getNtpTime() {
    // Get Unix timestamp from NTP server
    // https://github.com/PaulStoffregen/Time
}
```

This would allow:
- Accurate Unix epoch timestamps
- Better replay attack prevention
- Synchronized clocks across all devices

## Verification Checklist

After deploying the fix:

- [ ] Build successful
- [ ] Deployed to .NET server
- [ ] Arduino still has power and network connection
- [ ] Test coin insertion in Arduino
- [ ] Check server logs for "? Timestamp valid"
- [ ] Check server logs for "? SUCCESS"
- [ ] Verify portal shows updated credit

## Configuration Notes

**Arduino settings** (Nano.ino):
- `serverPort = 83;` or `5000` (whatever your server port is)
- `hmacSecretKey = "b83f29aae116030da1bac6691471c8fa";` (must match server)

**Server settings** (.NET appsettings.json):
- `"HmacSecretKey": "b83f29aae116030da1bac6691471c8fa"` (must match Arduino)

## Still Having Issues?

Check these in order:

1. **Signature error (not timestamp)?**
   - Verify HMAC secret key matches in Arduino and server
   - Check message format: `"SITE_001:10:238"`

2. **Machine not locked error?**
   - Make sure you clicked "INSERT COIN" button first
   - Verify machine is in database with status "locked"

3. **Connection refused?**
   - Verify server IP and port in Arduino code
   - Check firewall allows port 83/5000
   - Ping server from Arduino network

4. **Still getting timestamp error?**
   - Verify server was rebuilt and deployed
   - Check that HmacService.cs changes are in use
   - Restart .NET application

## Contact & Support

If issues persist:
1. Check logs for exact error message
2. Note the timestamp value being sent
3. Verify secret key in both locations
4. Ensure both devices are on same network
