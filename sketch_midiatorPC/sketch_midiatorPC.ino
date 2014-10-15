#include <Servo.h>

// pinout
int led = 13;
int dirPinA = 3;
int stepperPinA = 2;
int dirPinB = 7;
int stepperPinB = 6;
int servo1pin = 10;
int servo2pin = 11;
int stepperSleepA = A2;
int stepperSleepB = A0;

Servo servo1, servo2;

// blinky
int ledState;

int rPIOK = 0;

void setup() {
  pinMode(led, OUTPUT);
  pinMode(dirPinA, OUTPUT);
  pinMode(stepperPinA, OUTPUT);
  pinMode(stepperSleepA, OUTPUT);
  
  pinMode(dirPinB, OUTPUT);
  pinMode(stepperPinB, OUTPUT);
  pinMode(stepperSleepB, OUTPUT);
  
  servo1.attach(servo1pin);
  servo1.write(0);
  
  servo2.attach(servo2pin);
  servo2.write(20);
  
  Serial.begin(9600);
  Serial.println("Console");  
  
  Serial2.begin(9600); // RPi on RX2
}

// timers and counters
int steppedA, steppedB; // did we raise the step pin last loop?
unsigned long intA, intB; // usec interval between steps
unsigned long nextA, nextB; // usec next step scheduled at

char voices[8];
int voiceA = 0, voiceB = 1, voiceServo1 = 2, voiceServo2 = 3;
int lastServo1 = 0, lastServo2 = 20;
unsigned long lastMove;
int asleep = 0;

unsigned long pitches[256] = {0,115447,108967,102851,97079,91630,86487,81633,77051,72727,68645,64792,61156,57723,54483,51425,48539,45815,43243,40816,38525,36363,34322,32396,30578,28861,27241,25712,24269,22907,21621,20408,19262,18181,17161,16198,15289,14430,13620,12856,12134,11453,10810,10204,9631,9090,8580,8099,7644,7215,6810,6428,6067,5726,5405,5102,4815,4545,4290,4049,3822,3607,3405,3214,3033,2863,2702,2551,2407,2272,2145,2024,1911,1803,1702,1607,1516,1431,1351,1275,1203,1136,1072,1012,955,901,851,803,758,715,675,637,601,568,536,506,477,450,425,401,379,357,337,318,300,284,268,253,238,225,212,200,189,178,168,159,150,142,134,126,119,112,106,100,94,89,84,79,75,71,67,63,59,56,53,50,47,44,42,39,37,35,33,31,29,28,26,25,23,22,21,19,18,17,16,15,14,14,13,12,11,11,10,9,9,8,8,7,7,7,6,6,5,5,5,4,4,4,4,3,3,3,3,3,2,2,2,2,2,2,2,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};
// run once per loop()
void driverRun() {
  byte p;
  unsigned long now = micros();
  
  if (intA > 0 || intB > 0) {
    lastMove = millis();
    if (asleep) {
      Serial.println("wakeup");
      asleep = 0;
      digitalWrite(stepperSleepA, LOW);
      digitalWrite(stepperSleepB, LOW);
    }
  } else if (!asleep && millis() - lastMove > 2000) {
    Serial.println("sleeping");
    asleep = 1;
    digitalWrite(stepperSleepA, HIGH);
    digitalWrite(stepperSleepB, HIGH);
  }    
    

  if (steppedA) {
    digitalWrite(stepperPinA, LOW);
    steppedA = 0;
    
    nextA = now + intA;
  } else if (intA > 0) {
    if (now >= nextA) {
      digitalWrite(stepperPinA, HIGH);
      steppedA = 1;
      lastMove = millis();
    }
  } else {
    nextA = now + intA;
  }    

  if (steppedB) {
    digitalWrite(stepperPinB, LOW);
    steppedB = 0;

    nextB = now + intB;
  } else if (intB > 0) {
    if (now >= nextB) {
      digitalWrite(stepperPinB, HIGH);
      steppedB = 1;
      lastMove = millis();
    }
  } else {
    nextB = now + intB;
  }
  
  if (voices[voiceServo1] != lastServo1) {
    p = voices[voiceServo1];
    if (p > 0)
      servo1.write(20);
    else
      servo1.write(0);
    lastServo1 = voices[voiceServo1];
  }

  if (voices[voiceServo2] != lastServo2) {
    p = voices[voiceServo2];
    if (p > 0)
      servo2.write(0);
    else
      servo2.write(20);
    lastServo2 = voices[voiceServo2];
  }
}

int blinky = 0;

int beepstep = 0;
unsigned long ticks = 0;

char buf[8];
void loop() {
  char check;
  char temp[8];
  int i;
  
  if (!rPIOK) {
    ticks++;
    // periodic beeping
    if (ticks > 30*92) {
      ticks = 0;
      beepstep++;
    }
    if (beepstep == 0) {
      voices[voiceA] = 75;
    } else if (beepstep == 1) {
      voices[voiceA] = 0;
    }
    intA = pitches[voices[voiceA]];
    if (beepstep > 80)
      beepstep = 0;
  }

  driverRun();
  
  if (Serial.available() >= 10) {
    Serial.readBytes(voices, 8);
    intA = pitches[voices[voiceA]];
    intB = pitches[voices[voiceB]];    
  }
  while (Serial2.available() >= 10) {
    check = Serial2.read();
    if (check != (char)0xFE) continue;
    check = Serial2.read();
    if (check == (char)0xFD) {
      // standard note data
      Serial2.readBytes(voices, 8);
      intA = pitches[voices[voiceA]];
      intB = pitches[voices[voiceB]];    

      rPIOK = 1;
      blinky = 1;
      digitalWrite(led, HIGH);
    } else if (check == (char)0xFC) {
      // demo commands
      rPIOK = 1;
      Serial2.readBytes(buf, 8);
      if (!strcmp(buf, "estop")) {
        Serial.println("e-stop");
        intA = 0;
        intB = 0;
      } else if (!strcmp(buf, "step")) {
        Serial.println("step");
        digitalWrite(stepperPinA, HIGH);
        delay(1);
        digitalWrite(stepperPinA, LOW);
      } else if (!strcmp(buf, "sweep")) {
        Serial.println("sweep");
        for (int d = 1000; d > 0; d -= max(d/100, 5)) {
          digitalWrite(stepperPinA, HIGH);
          delay(1);
          digitalWrite(stepperPinA, LOW);
          delay(d);
          if (Serial2.available() > 0) {
            Serial.println("interrupted");
            break;
          }
        }
        Serial.println("sweep done");
      } else if (buf[0] == 'a') {
        intA = atol(buf+1);
        Serial.print("got intA "); Serial.println(intA, DEC);
      } else if (buf[0] == 'b') {
        intB = atol(buf+1);
        Serial.print("got intB "); Serial.println(intB, DEC);
      }
    }
  }
  
  if (blinky > 0)
    blinky++;
  if (blinky > 1000) {
    digitalWrite(led, LOW);
    blinky = 0;
  }
}


