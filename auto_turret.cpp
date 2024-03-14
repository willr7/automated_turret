#include<Servo.h>

Servo serX;
Servo serY;
Servo serFire;

String serialData;

void setup() {
  serFire.attach(13);
  serX.attach(11);
  serY.attach(12);
  serFire.write(0);
  serX.write(0);
  serY.write(0);
  Serial.begin(9600);
  Serial.setTimeout(10);
}

void loop() {
  //lol
}

void serialEvent() {
  serialData = Serial.readString();
  serX.write(parseDataX(serialData, serX));
  serY.write(parseDataY(serialData, serY));
  serFire.write(parseDataFire(serialData, serFire));
}

int parseDataX(String data, Servo ser){
  data.remove(data.indexOf("Y"));
  data.remove(data.indexOf("X"), 1);
  if (data.toInt() <= 180){
    return data.toInt();
  }
  else {
    return ser.read();
  }
}

int parseDataY(String data, Servo ser){
  data.remove(0,data.indexOf("Y") + 1);
  data.remove(data.indexOf("C"));
  if (data.toInt() <= 80){
    return data.toInt();
  }
  else {
    return ser.read();
  }
}

int parseDataFire(String data, Servo ser){
  data.remove(0, data.indexOf("C") + 1);
  if (data.toInt() == 13 || data.toInt() == 32 || data.toInt() == 70 || data.toInt() == 1){
    if (ser.read() + 45 >= 180) {
      return ser.read() - 45;
    }
    else {
      return ser.read() + 45;
    }
  } 
  else {
    return ser.read();
  }
}