"""
HMAC-SHA256 Test Script

This script tests the HMAC-SHA256 computation to verify both Arduino and Server are correct.

Usage:
    python hmac_test.py
"""

import hmac
import hashlib

# Configuration (MUST match Arduino and Server)
SECRET_KEY = "b83f29aae116030da1bac6691471c8fa"
MACHINE_ID = "SITE_001"

# Test Cases (from your serial monitor)
test_cases = [
    {
        "pulses": 1,
        "timestamp": 11,
        "arduino_signature": "03cba6bc267387d3f534e3993 6fa3f452dcca9c6036837c43afe7accba6b488",
        "expected_signature": None  # Will be computed
    },
    {
        "pulses": 10,
        "timestamp": 16,
        "arduino_signature": "e91098144fca17416bc30c9239313 64 68d2cfbafdeae73d7e2c64c163e88561",
        "expected_signature": None  # Will be computed
    }
]

def compute_hmac(machine_id, pulse_count, timestamp, secret_key):
    """Compute HMAC-SHA256 signature"""
    message = f"{machine_id}:{pulse_count}:{timestamp}"
    signature = hmac.new(
        secret_key.encode('utf-8'),
        message.encode('utf-8'),
        hashlib.sha256
    ).hexdigest()
    return message, signature

print("=" * 80)
print("HMAC-SHA256 Signature Verification")
print("=" * 80)
print(f"\nSecret Key: {SECRET_KEY}")
print(f"Machine ID: {MACHINE_ID}")
print(f"\nKey Length: {len(SECRET_KEY)} characters")
print(f"Secret Key as bytes: {len(SECRET_KEY.encode('utf-8'))} bytes")
print(f"Secret Key hex interpretation: ", end="")
try:
    # Try to decode as hex
    key_bytes = bytes.fromhex(SECRET_KEY)
    print(f"Valid hex string ({len(key_bytes)} bytes when decoded)")
except:
    print("Not valid hex (treating as ASCII string)")

print("\n" + "=" * 80)
print("Test Cases")
print("=" * 80)

for i, test in enumerate(test_cases, 1):
    pulses = test["pulses"]
    timestamp = test["timestamp"]
    arduino_sig = test["arduino_signature"].replace(" ", "").lower()
    
    message, expected_sig = compute_hmac(MACHINE_ID, pulses, timestamp, SECRET_KEY)
    
    print(f"\nTest Case {i}:")
    print(f"  Message: {message}")
    print(f"  Pulses: {pulses}, Timestamp: {timestamp}")
    print(f"\n  Computed Signature: {expected_sig}")
    print(f"  Arduino Signature:  {arduino_sig}")
    
    if expected_sig == arduino_sig:
        print(f"  ? MATCH! Arduino signature is CORRECT")
    else:
        print(f"  ? MISMATCH! Signatures do not match")
        print(f"\n  Comparison:")
        print(f"    Expected: {expected_sig}")
        print(f"    Got:      {arduino_sig}")
        
        # Show first difference
        for j, (e, a) in enumerate(zip(expected_sig, arduino_sig)):
            if e != a:
                print(f"    First difference at position {j}: expected '{e}', got '{a}'")
                break

# Also test what SERVER would compute
print("\n" + "=" * 80)
print("Server Computation (C#)")
print("=" * 80)

for i, test in enumerate(test_cases, 1):
    pulses = test["pulses"]
    timestamp = test["timestamp"]
    
    message, expected_sig = compute_hmac(MACHINE_ID, pulses, timestamp, SECRET_KEY)
    
    print(f"\nTest Case {i}:")
    print(f"  C# Code:")
    print(f'    var payload = "{MACHINE_ID}:{pulses}";')
    print(f'    var timestamp = "{timestamp}";')
    print(f'    HmacService.VerifySignature(payload, timestamp, "{SECRET_KEY}", "{expected_sig}");')
    print(f"\n  Expected Server Signature: {expected_sig}")

# Additional diagnostics
print("\n" + "=" * 80)
print("Diagnostics")
print("=" * 80)

print(f"\nSecret Key Analysis:")
print(f"  Length: {len(SECRET_KEY)} characters")
print(f"  Lowercase: {SECRET_KEY == SECRET_KEY.lower()}")
print(f"  Valid hex: ", end="")
try:
    bytes.fromhex(SECRET_KEY)
    print("Yes")
except:
    print("No - treating as ASCII string")

print(f"\nMessage Format Validation:")
test_message = f"{MACHINE_ID}:1:11"
print(f"  Format: {{machineId}}:{{pulseCount}}:{{timestamp}}")
print(f"  Example: {test_message}")
print(f"  Length: {len(test_message)} characters")

# Test with known values
print("\n" + "=" * 80)
print("Known Value Test (RFC 4231 Test Case)")
print("=" * 80)

# RFC 4231 Test Case 1
known_key = bytes.fromhex("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b")
known_data = b"Hi There"
known_signature = "b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7"

test_sig = hmac.new(known_key, known_data, hashlib.sha256).hexdigest()
print(f"\nRFC 4231 Test Case:")
print(f"  Key: {known_key.hex()}")
print(f"  Data: {known_data}")
print(f"  Expected: {known_signature}")
print(f"  Computed: {test_sig}")
print(f"  Result: {'? PASS' if test_sig == known_signature else '? FAIL'}")

print("\n" + "=" * 80)
