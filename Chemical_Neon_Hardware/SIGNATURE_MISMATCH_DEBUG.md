# HMAC Signature Mismatch - Debugging Guide

## Issue Summary
Server is rejecting HMAC signatures from Arduino with error:
```
? Invalid HMAC signature for machine SITE_001. Payload: SITE_001:3, Timestamp: 597
```

## Root Cause Analysis

The HMAC signature computation requires exact byte-for-byte matching:

### Arduino Computation
```cpp
message = "SITE_001:3:597"
hmac_sha256(secretKey, message) -> signature
signature in hex: "126502bea7710819..."
```

### Server Computation  
```csharp
message = "SITE_001:3:597"
ComputeSignature(payload, timestamp, secretKey) -> signature
signature in hex (should match): "126502bea7710819..."
```

## Key Differences to Check

### 1. **Hex Case Sensitivity** ? FIXED
- **Arduino**: Uses `sprintf("%02x", ...)` ? lowercase hex
  - Example: "a1b2c3d4"
- **Server (.NET 8)**: Uses `Convert.ToHexString()` ? uppercase hex
  - Example: "A1B2C3D4"
- **Fix**: Added `.ToLower()` to server signature computation

### 2. **Message Format**
Both should compute HMAC on: `"machineId:pulseCount:timestamp"`
- Example: `"SITE_001:3:597"`

### 3. **Secret Key Encoding**
Both treat secret key as UTF-8 string:
- Arduino: `(const uint8_t*)hmacSecretKey`
- Server: `Encoding.UTF8.GetBytes(secretKey)`

## Debugging Steps

### Step 1: Verify Secret Key Matches

**Arduino** (Nano.ino, line ~44):
```cpp
const char* hmacSecretKey = "b83f29aae116030da1bac6691471c8fa";
```

**Server** (appsettings.json):
```json
"HmacSecretKey": "b83f29aae116030da1bac6691471c8fa"
```

? Both should be identical (32-character hex string)

### Step 2: Check Serial Output

Upload updated Arduino code and check Serial Monitor:

```
?? Sending 3 pulses...
   Message: SITE_001:3:597
   Signature: 126502bea7710819a1b2c3d4e5f6a7b8
   Secret Key: b83f29aae116030da1bac6691471c8fa
   Payload size: 138 bytes
? Data sent successfully!
```

**Note the signature value (lowercase hex).**

### Step 3: Check Server Logs

After deploying updated server, insert coin and check logs:

```
[ReceiveCoin] Received request - Machine: SITE_001, Pulses: 3, Timestamp: 597, Signature: 126502bea7710819...
? Timestamp valid for SITE_001: 597
   Computing HMAC with message: 'SITE_001:3:597'
   Received signature: 126502bea7710819a1b2c3d4e5f6a7b8
   Computed signature: 126502bea7710819a1b2c3d4e5f6a7b8
   Match: True
? Signature valid for SITE_001
```

If `Match: True` ? ? Success!
If `Match: False` ? See next section

### Step 4: If Signatures Don't Match

Compare the two signatures in the logs:

**Example of mismatch:**
```
   Received signature: 126502bea7710819a1b2c3d4e5f6a7b8
   Computed signature: 224513cfb8820b2ab2c3d4e5f6a7b8c9
```

**Possible causes:**

1. **Secret keys don't match**
   - Copy exact hex from appsettings.json
   - Paste into Arduino code
   - No spaces or extra characters

2. **Message format differs**
   - Check that Arduino sends: `"SITE_001:3:597"` (with colons)
   - Server should reconstruct the same format
   - Look at server logs to see what message it computed

3. **Encoding issue**
   - Both should use UTF-8
   - No BOM (Byte Order Mark)
   - Check for hidden characters (spaces, newlines)

4. **Timestamp format**
   - Arduino sends as string: `"597"`
   - Server receives as string: `"597"`
   - They should match exactly

## Quick Checklist

- [ ] Arduino secret key matches server config
- [ ] Arduino sends message in format: `"MachineId:PulseCount:Timestamp"`
- [ ] Server built and deployed with updated HmacService.cs (lowercase hex)
- [ ] Serial monitor shows Arduino's computed signature
- [ ] Server logs show both computed and received signatures
- [ ] Log entries show "Match: True"

## Testing with Known Values

To test HMAC computation independently:

### Python Test Script
```python
import hmac
import hashlib

secret_key = "b83f29aae116030da1bac6691471c8fa"
message = "SITE_001:3:597"

signature = hmac.new(
    secret_key.encode('utf-8'),
    message.encode('utf-8'),
    hashlib.sha256
).hexdigest()

print(f"Message: {message}")
print(f"Secret Key: {secret_key}")
print(f"Computed Signature: {signature}")
# Expected: Should match Arduino and Server values
```

### C# Test
```csharp
using System.Security.Cryptography;
using System.Text;

var secretKey = "b83f29aae116030da1bac6691471c8fa";
var message = "SITE_001:3:597";

using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
{
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
    var signature = Convert.ToHexString(hash).ToLower();
    
    Console.WriteLine($"Message: {message}");
    Console.WriteLine($"Secret Key: {secretKey}");
    Console.WriteLine($"Computed Signature: {signature}");
    // Expected: Should match Arduino value
}
```

## Common Mistakes

? **Secret key case mismatch**
```
Arduino: "b83f29aae116030da1bac6691471c8fa"  (lowercase)
Server:  "B83F29AAE116030DA1BAC6691471C8FA"  (uppercase)
Result: SIGNATURE MISMATCH ?
```

? **Correct secret key**
```
Arduino: "b83f29aae116030da1bac6691471c8fa"
Server:  "b83f29aae116030da1bac6691471c8fa"
Result: SIGNATURE MATCH ?
```

? **Extra characters in message**
```
Arduino sends: "SITE_001:3:597"
Server expects: "SITE_001:3:597 " (note the space)
Result: SIGNATURE MISMATCH ?
```

## Deployment Steps

1. **Update Arduino**
   - Upload new Nano.ino with secret key logging
   - Check serial monitor during coin insertion
   - Note the signature value

2. **Update .NET Server**
   - Verify HmacService.cs has `.ToLower()` on hex conversion
   - Rebuild solution
   - Deploy to server
   - Restart application

3. **Test**
   - Insert coin in Arduino
   - Check Arduino Serial Monitor for message/signature
   - Check Server logs for computed/received signatures
   - Verify "Match: True"

## Success Criteria

? Timestamp validation passes
? Signature computed and received signatures match
? Machine found and locked in database
? Credit added successfully
? Portal shows updated balance

## Still Need Help?

Provide the following information:
1. Arduino Serial Monitor output (message, signature, secret key)
2. Server log output (computed vs received signatures)
3. The exact error message from server
4. Confirm secret keys are identical in both files
