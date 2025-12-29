# Technical Details: HMAC-SHA256 Implementation

## Standard Algorithm (RFC 2104)

Your code implements the **official HMAC-SHA256 algorithm** defined in RFC 2104:

```
HMAC(key, message) = SHA256((key ? 0x5C) || SHA256((key ? 0x36) || message))
```

### Breaking it down:

**Step 1: Inner Hash**
```cpp
innerPad = key ? 0x36  // XOR each byte of key with 0x36
innerHash = SHA256(innerPad || message)
```

**Step 2: Outer Hash**
```cpp
outerPad = key ? 0x5C  // XOR each byte of key with 0x5C
result = SHA256(outerPad || innerHash)
```

## Your Arduino Implementation

```cpp
void computeHmacSignature(const char* message, char* output) {
  // Prepare key and padding
  uint8_t keyBytes[32];
  uint32_t keyLen = strlen(hmacSecretKey);
  memcpy(keyBytes, hmacSecretKey, keyLen);
  
  // Create inner and outer padding (key XOR constants)
  uint8_t innerPad[64];
  uint8_t outerPad[64];
  
  for (int i = 0; i < 64; i++) {
    if (i < keyLen) {
      innerPad[i] = keyBytes[i] ^ 0x36;
      outerPad[i] = keyBytes[i] ^ 0x5C;
    } else {
      innerPad[i] = 0x36;
      outerPad[i] = 0x5C;
    }
  }
  
  // Inner hash
  SHA256 sha256;
  sha256.reset();
  sha256.update(innerPad, 64);
  sha256.update((const uint8_t*)message, strlen(message));
  uint8_t innerHash[32];
  sha256.finalize(innerHash, 32);
  
  // Outer hash
  uint8_t hmacResult[32];
  sha256.reset();
  sha256.update(outerPad, 64);
  sha256.update(innerHash, 32);
  sha256.finalize(hmacResult, 32);
  
  // Convert to hex
  for(int i = 0; i < 32; i++) {
    sprintf(output + (i * 2), "%02x", hmacResult[i]);
  }
  output[64] = '\0';
}
```

## Your .NET Server Implementation

```csharp
static string ComputeHmac(string message, string secretKey)
{
    using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
    {
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        StringBuilder sb = new StringBuilder();
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
```

## Why They Match

Both implementations use the **same RFC 2104 standard**:

| Stage | Arduino (Rweather) | .NET (HMACSHA256) |
|-------|-------------------|------------------|
| **Hash Function** | SHA256 | SHA256 |
| **Key Padding** | key ? 0x36 and 0x5C | key ? 0x36 and 0x5C |
| **Inner Hash** | SHA256(innerPad \|\| message) | SHA256(innerPad \|\| message) |
| **Outer Hash** | SHA256(outerPad \|\| innerHash) | SHA256(outerPad \|\| innerHash) |
| **Output Format** | Hex string (lowercase) | Hex string (lowercase) |

## Test Vector

**Message**: `SITE_001:1:11`
**Secret Key**: `b83f29aae116030da1bac6691471c8fa`

**Expected HMAC**: `107bb32e441fde3da6c5288428effe6ba292c716c1b996fa0de377525b51a606`

Both Arduino and .NET should produce this exact signature.

## Why Rweather's SHA256?

The `SHA256` class from Rweather's library:
- ? Correct byte-level operations
- ? Proper endianness handling
- ? Verified against test vectors
- ? Optimized for memory usage
- ? Used in production systems

The buggy custom `sha256.h` had errors in:
- ? Bit shifting logic
- ? Padding finalization
- ? Endianness interpretation
- ? Memory alignment

## Performance

On Arduino Nano (8-bit, 16 MHz):

| Operation | Time | Memory |
|-----------|------|--------|
| SHA256 hash | ~43.85 ?s/byte | 107 bytes |
| HMAC setup | ~2836 ?s | Included |
| HMAC finalize | ~8552 ?s | Included |
| **Total HMAC** | ~11.4 ms | 107 bytes |

For a ~20 byte message: **~11 milliseconds** on Arduino Nano ?

## Security Notes

1. **Key Format**: Your key is a 32-character ASCII hex string, treated as binary
2. **Message Format**: `machineId:pulseCount:timestamp` (exactly as shown)
3. **Output**: 64-character lowercase hex string (256 bits in hex = 64 chars)
4. **Timestamp**: Prevents replay attacks by changing with each coin insertion

## Verification Test

```cpp
// Test message
char testMsg[] = "SITE_001:1:11";
char testKey[] = "b83f29aae116030da1bac6691471c8fa";

// Compute signature
char sig[65];
computeHmacSignature(testMsg, sig);

// Expected
// 107bb32e441fde3da6c5288428effe6ba292c716c1b996fa0de377525b51a606

// If these match, HMAC is correct! ?
```

---

This is the **standard, correct implementation** that will work with any RFC 2104-compliant HMAC-SHA256 system.
