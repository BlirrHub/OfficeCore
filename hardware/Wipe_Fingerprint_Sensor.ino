#include <Adafruit_Fingerprint.h>

// ESP32 Hardware Serial 2 pins
#define RXD2 16
#define TXD2 17

HardwareSerial mySerial(2);
Adafruit_Fingerprint finger = Adafruit_Fingerprint(&mySerial);

void setup() {
  Serial.begin(9600);
  while (!Serial);
  delay(2000);
  Serial.println("\n\n--- Fingerprint Sensor Wipe Utility ---");
  
  mySerial.begin(57600, SERIAL_8N1, RXD2, TXD2);
  finger.begin(57600);
  
  if (finger.verifyPassword()) {
    Serial.println("Found fingerprint sensor!");
  } else {
    Serial.println("Did not find fingerprint sensor :(");
    while (1) { delay(1); }
  }

  Serial.println("Wiping database in 3 seconds...");
  delay(3000);

  finger.emptyDatabase();
  Serial.println("Database is now completely empty! :)");
  Serial.println("WIPE COMPLETE! You can now re-flash your main ESP32_Fingerprint_Device.ino program.");
}

void loop() {
  delay(1000);
}
