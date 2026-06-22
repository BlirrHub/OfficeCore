#include <Adafruit_Fingerprint.h>

// ESP32 Hardware Serial 2 pins
#define RXD2 16
#define TXD2 17

HardwareSerial mySerial(2);
Adafruit_Fingerprint finger = Adafruit_Fingerprint(&mySerial);

bool isEnrollMode = false;

void setup() {
  // Initialize communication with the PC via USB
  Serial.begin(9600);
  while (!Serial);

  // Initialize communication with the fingerprint sensor
  mySerial.begin(57600, SERIAL_8N1, RXD2, TXD2);
  finger.begin(57600);
  
  if (finger.verifyPassword()) {
    Serial.println("DEVICE_READY");
  } else {
    Serial.println("ERROR: Could not find fingerprint sensor.");
    while (1) { delay(1); }
  }
}

void loop() {
  // Check for commands from the PC (OfficeCore API Background Service)
  if (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    command.trim();
    
    if (command == "ENROLL") {
      isEnrollMode = true;
      Serial.println("MODE:ENROLLING");
    }
  }

  if (isEnrollMode) {
    enrollFingerprint();
  } else {
    checkAttendance();
  }
  
  delay(50);
}

void checkAttendance() {
  uint8_t p = finger.getImage();
  if (p != FINGERPRINT_OK) return; // No finger detected
  
  p = finger.image2Tz();
  if (p != FINGERPRINT_OK) return; // Bad image

  p = finger.fingerSearch();
  if (p == FINGERPRINT_OK) {
    // Found a match!
    Serial.print("ATTENDANCE:");
    Serial.println(finger.fingerID);
    delay(2000); // Wait so we don't trigger 10 times for one touch
  }
}

void enrollFingerprint() {
  // Find an empty slot
  int id = 1;
  while (id < 127) {
    if (finger.loadModel(id) != FINGERPRINT_OK) {
      break; // Found an empty slot
    }
    id++;
  }

  if (id > 127) {
    Serial.println("ERROR: Sensor is full!");
    isEnrollMode = false;
    return;
  }

  // Scan finger first time
  Serial.println("INFO: Waiting for valid finger to enroll");
  uint8_t p = -1;
  while (isEnrollMode) {
    p = finger.getImage();
    if (p == FINGERPRINT_OK) {
      p = finger.image2Tz(1);
      if (p == FINGERPRINT_OK) {
        break; // Good image captured!
      }
    }
    
    // Check if PC sent cancel command
    if (Serial.available() > 0) {
      String cmd = Serial.readStringUntil('\n');
      cmd.trim();
      if (cmd == "CANCEL") {
        isEnrollMode = false;
        return;
      }
    }
  }
  
  Serial.println("INFO: First scan successful. Remove finger.");
  delay(2000); // Give user 2 full seconds to realize the scan worked and lift their finger

  // Wait for finger to be removed
  p = 0;
  while (p != FINGERPRINT_NOFINGER) {
    p = finger.getImage();
  }
  
  Serial.println("INFO: Finger removed. Place SAME finger again.");
  // Scan finger second time
  p = -1;
  while (isEnrollMode) {
    p = finger.getImage();
    if (p == FINGERPRINT_OK) {
      p = finger.image2Tz(2);
      if (p == FINGERPRINT_OK) {
        break; // Good image captured!
      }
    }
  }

  Serial.println("INFO: Second scan successful. Creating model...");
  // Create model
  p = finger.createModel();
  if (p == FINGERPRINT_OK) {
    p = finger.storeModel(id);
    if (p == FINGERPRINT_OK) {
      Serial.print("ENROLL_SUCCESS:");
      Serial.println(id);
    } else {
      Serial.println("ERROR: Failed to store model.");
    }
  } else {
    Serial.println("ERROR: Fingerprints did not match.");
  }
  
  isEnrollMode = false;
}
