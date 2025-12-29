/*
 * Wi-Fi Vendo Machine - Coin Sender (v3)
 * Hardware: Arduino Nano + W5500 Ethernet Shield + Coin Acceptor
 * 
 * UPDATES IN v3:
 * - Uses HMAC-SHA256 signature verification (secure)
 * - Sends timestamp to prevent replay attacks
 * - Uses Rweather's Arduino Cryptography Library (professional, battle-tested)
 * 
 * --- WIRING GUIDE ---
 * Coin Signal Wire:  D3 (Must be D2 or D3 for interrupts)
 * Ethernet CS Pin:   D10 (Standard for most shields)
 * Ethernet MOSI:     D11
 * Ethernet MISO:     D12
 * Ethernet SCK:      D13
 * Status LED:        D7 (Optional)
 * 
 * !!! SAFETY WARNING !!!
 * Ensure your Coin Acceptor signal does NOT send 12V to the Arduino.
 * Use a diode or voltage divider if the signal is powered.
 * 
 * !!! LIBRARY REQUIREMENT !!!
 * Install Rweather's "Crypto" library:
 * https://github.com/rweather/arduinolibs
 * Or via Arduino IDE: Sketch ? Include Library ? Manage Libraries ? Search "Crypto" by Rweather
 */

#include <SPI.h>
#include <Ethernet.h>
#include <Crypto.h>
#include <SHA256.h>

// ==========================================
//      HARDWARE CONFIGURATION (EDIT HERE)
// ==========================================

// 1. PIN DEFINITIONS
#define COIN_PIN        3    // MUST be 2 or 3 on Nano/Uno
#define ETHERNET_CS_PIN 10   // Default is 10. Change if your shield uses 8 or 4.
#define STATUS_LED_PIN  7    // LED that blinks when data is sent

// 2. COIN SLOT SETTINGS
#define INTERRUPT_TRIGGER FALLING 
#define PULSE_DELAY_MS    200     // Debounce time to separate distinct coins

// 3. NETWORK SETTINGS
byte mac[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xFE, 0xED };
IPAddress serverIp(192, 168, 1, 16);  // The local IP of your .NET Server PC
int serverPort = 83;                // Usually 80, 8080, or 5000

// 4. SECURITY - HMAC Configuration
// MUST match the HmacSecretKey in your .NET appsettings.json
const char* machineId = "SITE_001";
const char* hmacSecretKey = "b83f29aae116030da1bac6691471c8fa";

// ==========================================
//      END OF CONFIGURATION
// ==========================================

EthernetClient client;
volatile int pulseCount = 0;
unsigned long lastPulseTime = 0;
bool coinInserted = false;
unsigned long systemStartTime = 0;

void setup() {
  Serial.begin(9600);
  
  systemStartTime = millis();
  
  Serial.println(F("\n========================================"));
  Serial.println(F("Wi-Fi Vendo v3 - HMAC Secured"));
  Serial.println(F("========================================"));
  
  // Setup Pins
  pinMode(STATUS_LED_PIN, OUTPUT);
  pinMode(COIN_PIN, INPUT_PULLUP);
  
  // Attach Interrupt
  attachInterrupt(digitalPinToInterrupt(COIN_PIN), onCoinPulse, INTERRUPT_TRIGGER);

  Serial.println(F("Initializing Ethernet..."));
  
  // Initialize Ethernet with specific CS pin
  Ethernet.init(ETHERNET_CS_PIN);
  
  if (Ethernet.begin(mac) == 0) {
    Serial.println(F("ERROR: Failed to configure Ethernet using DHCP"));
    while (true) {
      digitalWrite(STATUS_LED_PIN, HIGH); delay(100);
      digitalWrite(STATUS_LED_PIN, LOW); delay(100);
    }
  }
  
  Serial.print(F("? Connected! Device IP: "));
  Serial.println(Ethernet.localIP());
  Serial.print(F("? Server: "));
  Serial.print(serverIp);
  Serial.print(F(":"));
  Serial.println(serverPort);
  Serial.print(F("? Machine ID: "));
  Serial.println(machineId);
  Serial.println(F("? Using Rweather Crypto Library (HMAC-SHA256)"));
  
  // Blink LED to indicate ready
  for(int i=0; i<3; i++) {
    digitalWrite(STATUS_LED_PIN, HIGH); delay(200);
    digitalWrite(STATUS_LED_PIN, LOW); delay(200);
  }
  
  Serial.println(F("Ready for coins!\n"));
}

void loop() {
  // Check if coin was inserted and debounce time has passed
  if (coinInserted && (millis() - lastPulseTime > PULSE_DELAY_MS)) {
    
    int pulsesToSend = 0;
    
    // Disable interrupts briefly to read/reset the volatile counter safely
    noInterrupts();
    pulsesToSend = pulseCount;
    pulseCount = 0;
    coinInserted = false;
    interrupts();

    if (pulsesToSend > 0) {
      sendCoinData(pulsesToSend);
    }
  }
}

// Interrupt Service Routine (Runs immediately when coin passes)
void onCoinPulse() {
  pulseCount++;
  lastPulseTime = millis();
  coinInserted = true;
}

// ==========================================
//      HMAC-SHA256 SIGNATURE COMPUTATION
// ==========================================

/**
 * Computes HMAC-SHA256 signature using Rweather's Crypto library
 * and converts to hex string
 * Message format: "machineId:pulseCount:timestamp"
 */
void computeHmacSignature(const char* message, char* output) {
  uint8_t keyBytes[32];
  uint32_t keyLen = strlen(hmacSecretKey);
  
  // Copy secret key (it's an ASCII string)
  memcpy(keyBytes, hmacSecretKey, keyLen);
  
  uint8_t hmacResult[32];
  
  // Create SHA256 hasher and HMAC it with the key
  // Rweather's library uses a different API: Hash object + manual HMAC computation
  SHA256 sha256;
  
  // HMAC-SHA256 algorithm:
  // 1. Create inner padding: key XOR 0x36
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
  
  // 2. Inner hash: hash(innerPad + message)
  sha256.reset();
  sha256.update(innerPad, 64);
  sha256.update((const uint8_t*)message, strlen(message));
  uint8_t innerHash[32];
  sha256.finalize(innerHash, 32);
  
  // 3. Outer hash: hash(outerPad + innerHash)
  sha256.reset();
  sha256.update(outerPad, 64);
  sha256.update(innerHash, 32);
  sha256.finalize(hmacResult, 32);
  
  // Convert to hex string
  for(int i = 0; i < 32; i++) {
    sprintf(output + (i * 2), "%02x", hmacResult[i]);
  }
  output[64] = '\0';
}

void sendCoinData(int pulses) {
  Serial.print(F(">> Sending "));
  Serial.print(pulses);
  Serial.println(F(" pulses..."));
  
  digitalWrite(STATUS_LED_PIN, HIGH);

  // Get current timestamp (seconds since device started)
  unsigned long timestamp = getTimestamp();
  
  // Build message for HMAC: "machineId:pulseCount:timestamp"
  char message[64];
  snprintf(message, sizeof(message), "%s:%d:%lu", machineId, pulses, timestamp);
  
  // Compute HMAC-SHA256 signature
  char signature[65]; // 64 hex chars + null terminator
  computeHmacSignature(message, signature);
  
  Serial.print(F("   Message: "));
  Serial.println(message);
  Serial.print(F("   Signature: "));
  Serial.println(signature);
  Serial.print(F("   Secret Key: "));
  Serial.println(hmacSecretKey);

  // Attempt connection
  if (client.connect(serverIp, serverPort)) {
    // Construct JSON Payload with HMAC signature
    String jsonPayload = "{";
    jsonPayload += "\"machineId\":\"" + String(machineId) + "\",";
    jsonPayload += "\"pulseCount\":" + String(pulses) + ",";
    jsonPayload += "\"timestamp\":\"" + String(timestamp) + "\",";
    jsonPayload += "\"signature\":\"" + String(signature);
    jsonPayload += "\"}";

    Serial.print(F("   Payload size: "));
    Serial.print(jsonPayload.length());
    Serial.println(F(" bytes"));

    // Send HTTP POST
    client.println(F("POST /api/hardware/coin HTTP/1.1"));
    client.print(F("Host: "));
    client.print(serverIp);
    client.print(F(":"));
    client.println(serverPort);
    client.println(F("Content-Type: application/json"));
    client.println(F("Connection: close"));
    client.print(F("Content-Length: "));
    client.println(jsonPayload.length());
    client.println();
    client.println(jsonPayload);
    
    Serial.println(F("? Data sent successfully!"));
    client.stop();
  } else {
    Serial.println(F("? Connection Failed. Check Server IP/Port."));
  }
  
  digitalWrite(STATUS_LED_PIN, LOW);
  Serial.println();
}

// ==========================================
//      TIMESTAMP FUNCTION
// ==========================================

/**
 * Returns elapsed seconds since device startup
 * For production use, implement NTP synchronization
 */
unsigned long getTimestamp() {
  return (millis() - systemStartTime) / 1000;
}