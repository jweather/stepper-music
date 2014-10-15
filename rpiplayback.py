#!/usr/bin/python

import serial
import time
import signal
import sys
import RPi.GPIO as GPIO

GPIO.setmode(GPIO.BOARD)
blueBtn = 16
GPIO.setup(blueBtn, GPIO.IN, pull_up_down=GPIO.PUD_UP)
GPIO.add_event_detect(blueBtn, GPIO.FALLING, bouncetime=200)

port = serial.Serial("/dev/ttyAMA0", baudrate=9600, timeout=3.0)

def sigint(signal, frame):
	port.write("\xFE\xFD\x00\x00\x00\x00\x00\x00\x00\x00")
	sys.exit(0)

signal.signal(signal.SIGINT, sigint)

time.sleep(2)

while 1:
	found = 0
	while not found:
		for i in range(0, 2):
			port.write("\xFE\xFD\x4D\x00\x00\x00\x00\x00\x00\x00")
			time.sleep(0.1)
			port.write("\xFE\xFD\x00\x00\x00\x00\x00\x00\x00\x00")
			time.sleep(0.1)
		# ugly way to wait 5 seconds for button press
		for i in range(0, 10):
			time.sleep(0.5)
			if GPIO.event_detected(blueBtn):
				found = 1
				break

	port.write("\xFE\xFD\x4F\x00\x00\x00\x00\x00\x00\x00")
	time.sleep(0.1)
	port.write("\xFE\xFD\x00\x00\x00\x00\x00\x00\x00\x00")


	f = open("sweetchild.rpi")
	start = int(round(time.time()*1000.0))
	for line in f:
		fields = line.rstrip().split(' ', 1)
		etime = int(fields[0])
		voices = fields[1].split(' ')
		now = int(round(time.time()*1000.0))
		now = now - start
		if now < etime:
			time.sleep((etime - now)/1000.0)
	
		print '@' + str(etime) + ' ' + ' '.join(voices)
		port.write("\xFE\xFD")
		for i in range(0, 8):
			port.write(chr(int(voices[i])))

		if GPIO.event_detected(blueBtn):
			break

	f.close()
