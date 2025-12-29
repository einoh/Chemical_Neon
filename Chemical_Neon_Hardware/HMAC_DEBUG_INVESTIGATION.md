# HMAC-SHA256 Arduino Implementation Bug Fix

## Problem Found

The Arduino's HMAC-SHA256 implementation has a bug in the key preprocessing step.

### The Bug (sha256.h, line ~228-233)

```cpp
// If key is longer than block size, hash it
if (keylen > 64) {
    sha256_init(&ctx);
    sha256_update(&ctx, key, keylen);
    sha256_final(&ctx, pad);      // ? pad now has the hash
    keylen = 32;                   // ? keylen is now 32
} else {
    memcpy(pad, key, keylen);      // ? This is NEVER executed if key > 64!
}
```

**Issue:** The `memcpy` call is in the `else` block, so if the key is longer than 64 bytes and gets hashed, the `pad` array contains the hash result (32 bytes) but the `memcpy` never happens.

However, in our case, the secret key is 32 bytes (hex string), so this shouldn't trigger... **UNLESS** the key is being read as something else.

## Most Likely Issue: Key Encoding

The Arduino secret key is defined as:
```cpp
const char* hmacSecretKey = "b83f29aae116030da1bac6691471c8fa";
```

This is a **hexadecimal string representation** of the key, not the raw binary key!

- Arduino treats it as: 32-character string ? 32 bytes
- Server treats it as: 32-character string ? 32 bytes
- But both are computing HMAC of the **hex string**, not the decoded binary!

### Example:
- Hex string: `"b83f29aae116030da1bac6691471c8fa"`
- As ASCII bytes: `{0x62, 0x38, 0x33, 0x66, ...}` (the digits 'b', '8', '3', 'f', etc.)
- Not the binary: `{0xb8, 0x3f, 0x29, 0xaa, ...}`

**This is actually CORRECT and matches both sides**, so this isn't the issue.

## Alternative: Missing Outer Loop Context

Wait - looking at the logs again:

```
Message: 'SITE_001:10:16'
Received signature: e9109814...
Computed signature: 87b322e7...
```

These are COMPLETELY different. Let me create a test scenario.

## Solution: Use Online HMAC Tester

To verify what the correct signature should be:

1. Go to: https://www.tools4noobs.com/online_tools/hmac/
2. Enter:
   - **Key:** `b83f29aae116030da1bac6691471c8fa`
   - **Message:** `SITE_001:10:16`
   - **Algorithm:** SHA256
3. Copy the result (should be lowercase hex)

## Simplified Arduino HMAC Fix

Instead of using the complex implementation, use a more proven approach. However, the real issue might be something simpler.

Let me first check: Can you verify if the Arduino is using a **different secret key than what's in appsettings.json**?

## Debugging Steps

1. **Check Arduino Serial Monitor Output:**
   Look for the line: `Secret Key: b83f29aae116030da1bac6691471c8fa`
   - If this shows a DIFFERENT key, that's the problem!
   - If it's the same, then the HMAC implementation has a bug

2. **Verify Server Config:**
   Check `appsettings.json` to confirm:
   ```json
   "HmacSecretKey": "b83f29aae116030da1bac6691471c8fa"
   ```

3. **Online Test:**
   - Use the online tool above to compute the "correct" signature
   - Compare with both Arduino and Server logs

## Most Likely Scenario

The Arduino might be using a **DIFFERENT secret key** than the server. This could happen if:
1. Arduino code was updated to a different key
2. Arduino is reading from EEPROM or persistent storage (different key there)
3. Typo in the secret key between files

**Quick Fix:** Share the Arduino Serial Monitor output and we can compare the keys directly.
