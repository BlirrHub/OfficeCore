#include <Adafruit_Fingerprint.h>
#include <WiFi.h>
#include <Preferences.h>

// ESP32 Hardware Serial 2 pins
#define RXD2 16
#define TXD2 17

HardwareSerial mySerial(2);
Adafruit_Fingerprint finger = Adafruit_Fingerprint(&mySerial);
Preferences preferences;
WiFiClient client;

bool isEnrollMode = false;
bool wifiEnabled = false;

String ssid = "";
String password = "";
String serverIp = "";
const int serverPort = 9000;

void setup() {
  // Initialize communication with the PC via USB
  Serial.begin(9600);
  while (!Serial);

  // Load saved Wi-Fi credentials
  preferences.begin("fingerprint", false);
  ssid = preferences.getString("ssid", "");
  password = preferences.getString("password", "");
  serverIp = preferences.getString("serverIp", "");
  
  // Attempt Wi-Fi connection if credentials exist
  if (ssid != "" && serverIp != "") {
    wifiEnabled = true;
    Serial.println("INFO: WiFi credentials found. Connecting...");
    WiFi.begin(ssid.c_str(), password.c_str());
    
    int attempts = 0;
    while (WiFi.status() != WL_CONNECTED && attempts < 20) {
      delay(500);
      Serial.print(".");
      attempts++;
    }
    Serial.println();
    
    if (WiFi.status() == WL_CONNECTED) {
      Serial.println("INFO: WiFi Connected.");
      Serial.print("INFO: IP Address: ");
      Serial.println(WiFi.localIP());
    } else {
      Serial.println("ERROR: WiFi Connection Failed. Falling back to Wired mode only.");
      wifiEnabled = false;
    }
  } else {
    Serial.println("INFO: No WiFi credentials found. Starting in Wired mode.");
  }

  // Initialize communication with the fingerprint sensor
  mySerial.begin(57600, SERIAL_8N1, RXD2, TXD2);
  finger.begin(57600);
  
  if (finger.verifyPassword()) {
    sendMessage("DEVICE_READY");
  } else {
    Serial.println("ERROR: Could not find fingerprint sensor.");
    while (1) { delay(1); }
  }
}

void loop() {
  handleTCP();
  handleSerial();

  if (isEnrollMode) {
    enrollFingerprint();
  } else {
    checkAttendance();
  }
  
  delay(50);
}

unsigned long lastTcpRetry = 0;
const unsigned long TCP_RETRY_INTERVAL = 5000;

// Sends message to BOTH Serial and TCP server if connected
void sendMessage(String msg) {
  Serial.println(msg); 
  
  if (wifiEnabled && WiFi.status() == WL_CONNECTED) {
    if (!client.connected()) {
      unsigned long currentMillis = millis();
      if (currentMillis - lastTcpRetry > TCP_RETRY_INTERVAL || lastTcpRetry == 0) {
        lastTcpRetry = currentMillis;
        Serial.println("INFO: Attempting to connect to TCP Server " + serverIp + ":" + String(serverPort));
        if (client.connect(serverIp.c_str(), serverPort)) {
            Serial.println("INFO: TCP Connection established!");
        } else {
            Serial.println("ERROR: TCP Connection failed! Retrying in 5s.");
        }
      }
    }
    
    if (client.connected()) {
      client.println(msg);
    }
  }
}

void handleCommand(String command) {
  command.trim();
  if (command == "") return;
  
  if (command == "ENROLL") {
    isEnrollMode = true;
    sendMessage("MODE:ENROLLING");
  } 
  else if (command == "CANCEL") {
    isEnrollMode = false;
    sendMessage("INFO: Enrollment cancelled.");
  }
  else if (command == "WIPE_SENSOR") {
    uint8_t p = finger.emptyDatabase();
    if (p == FINGERPRINT_OK) {
      sendMessage("INFO: Sensor wiped. All fingerprints deleted.");
    } else {
      sendMessage("ERROR: Failed to wipe sensor.");
    }
  }
  else if (command.startsWith("DELETE_MODEL:")) {
    int slotId = command.substring(13).toInt();
    uint8_t p = finger.deleteModel(slotId);
    if (p == FINGERPRINT_OK) {
      sendMessage("INFO: Deleted fingerprint slot " + String(slotId));
    } else {
      sendMessage("ERROR: Failed to delete slot " + String(slotId));
    }
  }
  else if (command.startsWith("SET_WIFI:")) {
    // Expected format: SET_WIFI:MySSID,MyPassword,192.168.1.100
    String data = command.substring(9);
    int firstComma = data.indexOf(',');
    int secondComma = data.lastIndexOf(',');
    
    if (firstComma != -1 && secondComma != -1 && firstComma != secondComma) {
      String newSsid = data.substring(0, firstComma);
      String newPass = data.substring(firstComma + 1, secondComma);
      String newIp = data.substring(secondComma + 1);
      
      preferences.putString("ssid", newSsid);
      preferences.putString("password", newPass);
      preferences.putString("serverIp", newIp);
      
      Serial.println("INFO: WiFi Configuration Saved. Rebooting in 2 seconds...");
      delay(2000);
      ESP.restart();
    } else {
      Serial.println("ERROR: Invalid WiFi config format. Use SET_WIFI:ssid,pass,ip");
    }
  }
  else if (command == "RESET_WIFI") {
    preferences.clear();
    Serial.println("INFO: WiFi Configuration Cleared. Rebooting in 2 seconds...");
    delay(2000);
    ESP.restart();
  }
}

void handleSerial() {
  if (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    handleCommand(command);
  }
}

void handleTCP() {
  if (!wifiEnabled || WiFi.status() != WL_CONNECTED) return;
  
  if (!client.connected()) {
    client.connect(serverIp.c_str(), serverPort);
  }
  
  if (client.connected() && client.available()) {
    String command = client.readStringUntil('\n');
    handleCommand(command);
  }
}

void checkAttendance() {
  uint8_t p = finger.getImage();
  if (p != FINGERPRINT_OK) return; 
  
  p = finger.image2Tz();
  if (p != FINGERPRINT_OK) return; 

  p = finger.fingerSearch();
  if (p == FINGERPRINT_OK) {
    sendMessage("ATTENDANCE:" + String(finger.fingerID));
    delay(2000); // Wait so we don't trigger 10 times for one touch
  } else if (p == FINGERPRINT_NOTFOUND) {
    sendMessage("INFO: Fingerprint not recognized. Try again.");
    delay(1000);
  }
}

void enrollFingerprint() {
  int id = 1;
  while (id < 127) {
    if (finger.loadModel(id) != FINGERPRINT_OK) {
      break; 
    }
    id++;
  }

  if (id > 127) {
    sendMessage("ERROR: Sensor is full!");
    isEnrollMode = false;
    return;
  }

  while (isEnrollMode) {
    sendMessage("INFO: Waiting for valid finger to enroll");
    uint8_t p = -1;
    while (isEnrollMode) {
      p = finger.getImage();
      if (p == FINGERPRINT_OK) {
        p = finger.image2Tz(1);
        if (p == FINGERPRINT_OK) break;
      }
      
      if (Serial.available() > 0) {
        String cmd = Serial.readStringUntil('\n');
        cmd.trim();
        if (cmd == "CANCEL") {
          isEnrollMode = false;
          sendMessage("INFO: Enrollment cancelled.");
          return;
        }
      }
      if (wifiEnabled && client.connected() && client.available()) {
         String cmd = client.readStringUntil('\n');
         cmd.trim();
         if (cmd == "CANCEL") {
           isEnrollMode = false;
           sendMessage("INFO: Enrollment cancelled.");
           return;
         }
      }
    }
    
    if (!isEnrollMode) return; 

    sendMessage("INFO: First scan successful. Remove finger.");
    delay(2000); 

    p = 0;
    while (p != FINGERPRINT_NOFINGER) {
      p = finger.getImage();
    }
    
    sendMessage("INFO: Finger removed. Place SAME finger again.");
    p = -1;
    while (isEnrollMode) {
      p = finger.getImage();
      if (p == FINGERPRINT_OK) {
        p = finger.image2Tz(2);
        if (p == FINGERPRINT_OK) break;
      }
      
      if (Serial.available() > 0) {
        String cmd = Serial.readStringUntil('\n');
        cmd.trim();
        if (cmd == "CANCEL") {
          isEnrollMode = false;
          sendMessage("INFO: Enrollment cancelled.");
          return;
        }
      }
      if (wifiEnabled && client.connected() && client.available()) {
         String cmd = client.readStringUntil('\n');
         cmd.trim();
         if (cmd == "CANCEL") {
           isEnrollMode = false;
           sendMessage("INFO: Enrollment cancelled.");
           return;
         }
      }
    }

    if (!isEnrollMode) return; 

    sendMessage("INFO: Processing...");
    p = finger.createModel();
    if (p == FINGERPRINT_OK) {
      p = finger.storeModel(id);
      if (p == FINGERPRINT_OK) {
        sendMessage("INFO: Second scan successful. Fingerprint enrolled!");
        sendMessage("ENROLL_SUCCESS:" + String(id));
        isEnrollMode = false;
        // Wait for finger to be removed so checkAttendance() doesn't fire immediately
        sendMessage("INFO: Please remove your finger.");
        p = 0;
        while (p != FINGERPRINT_NOFINGER) {
          p = finger.getImage();
        }
        delay(1000);
        return;
      } else {
        sendMessage("ERROR: Failed to store model.");
        isEnrollMode = false;
        return;
      }
    } else if (p == FINGERPRINT_ENROLLMISMATCH) {
      sendMessage("INFO: Fingerprints did not match. Please try again.");
      delay(1500);
    } else {
      sendMessage("INFO: Could not create model (code " + String(p) + "). Try again.");
      delay(1500);
    }
  }
}
