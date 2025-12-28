/*
 * Wi-Fi Vendo Machine - Coin Sender (v2)
 * Hardware: Arduino Nano + W5500 Ethernet Shield + Coin Acceptor
 * * --- WIRING GUIDE ---
 * Coin Signal Wire:  D2 (Must be D2 or D3 for interrupts)
 * Ethernet CS Pin:   D10 (Standard for most shields)
 * Ethernet MOSI:     D11
 * Ethernet MISO:     D12
 * Ethernet SCK:      D13
 * Status LED:        D5 (Optional)
 * * !!! SAFETY WARNING !!!
 * Ensure your Coin Acceptor signal does NOT send 12V to the Arduino.
 * Use a diode or voltage divider if the signal is powered.
 */

#include <SPI.h>
#include <Ethernet.h>

// ==========================================
//      HARDWARE CONFIGURATION (EDIT HERE)
// ==========================================

// 1. PIN DEFINITIONS
#define COIN_PIN        3    // MUST be 2 or 3 on Nano/Uno
#define ETHERNET_CS_PIN 10   // Default is 10. Change if your shield uses 8 or 4.
#define STATUS_LED_PIN  7    // LED that blinks when data is sent

// 2. COIN SLOT SETTINGS
// Most slots are "Normally Open" (NO). They pull the line LOW when a coin drops.
// If your blueprint uses "Normally Closed" (NC), change to RISING.
#define INTERRUPT_TRIGGER FALLING 
#define PULSE_DELAY_MS    200     // Debounce time to separate distinct coins

// 3. NETWORK SETTINGS
byte mac[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xFE, 0xED }; // Change if using multiple machines
IPAddress serverIp(192, 168, 1, 16); // The local IP of your .NET Server PC
int serverPort = 83;                  // Usually 80 or 8080 or 5000

// 4. SECURITY
const char* machineId = "SITE_001";
const char* apiKey    = "SECRET_API_KEY_123";

// ==========================================
//      END OF CONFIGURATION
// ==========================================

EthernetClient client;
volatile int pulseCount = 0;
unsigned long lastPulseTime = 0;
bool coinInserted = false;

void setup() {
  Serial.begin(9600);
  
  // Setup Pins
  pinMode(STATUS_LED_PIN, OUTPUT);
  
  // INPUT_PULLUP is safer for N.O. switches (prevents floating signals)
  pinMode(COIN_PIN, INPUT_PULLUP);
  
  // Attach Interrupt
  attachInterrupt(digitalPinToInterrupt(COIN_PIN), onCoinPulse, INTERRUPT_TRIGGER);

  Serial.println(F("Initializing Ethernet..."));
  
  // Initialize Ethernet with specific CS pin
  Ethernet.init(ETHERNET_CS_PIN);
  
  if (Ethernet.begin(mac) == 0) {
    Serial.println(F("Failed to configure Ethernet using DHCP"));
    // Optional: Fallback to static IP if DHCP fails
    // Ethernet.begin(mac, ip);
    while (true) {
      digitalWrite(STATUS_LED_PIN, HIGH); delay(100);
      digitalWrite(STATUS_LED_PIN, LOW); delay(100);
    }
  }
  
  Serial.print(F("Connected! Device IP: "));
  Serial.println(Ethernet.localIP());
  
  // Blink LED to indicate ready
  for(int i=0; i<3; i++) {
    digitalWrite(STATUS_LED_PIN, HIGH); delay(200);
    digitalWrite(STATUS_LED_PIN, LOW); delay(200);
  }
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

void sendCoinData(int pulses) {
  Serial.print(F("Sending pulses: "));
  Serial.println(pulses);
  digitalWrite(STATUS_LED_PIN, HIGH);

  if (client.connect(serverIp, serverPort)) {
    // Construct JSON Payload
    // Note: Using String class for simplicity, but avoid heap fragmentation in large apps
    String jsonPayload = "{";
    jsonPayload += "\"machineId\":\"" + String(machineId) + "\",";
    jsonPayload += "\"apiKey\":\"" + String(apiKey) + "\",";
    jsonPayload += "\"pulseCount\":" + String(pulses);
    jsonPayload += "}";

    // Send HTTP POST
    client.println(F("POST /api/vending/hardware/coin HTTP/1.1"));
    client.print(F("Host: "));
    client.println(serverIp);
    client.println(F("Content-Type: application/json"));
    client.println(F("Connection: close"));
    client.print(F("Content-Length: "));
    client.println(jsonPayload.length());
    client.println();
    client.println(jsonPayload);
    
    // Read response (optional, for debugging)
    /*
    while(client.connected()) {
      if(client.available()) {
        char c = client.read();
        Serial.print(c);
      }
    }
    */
    
    Serial.println(F("Data sent."));
    client.stop();
  } else {
    Serial.println(F("Connection Failed. Check Server IP/Port."));

  }
  
  digitalWrite(STATUS_LED_PIN, LOW);
}