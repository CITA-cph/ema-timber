try:
    import RPi.GPIO as gpio
except:
    pass
import time, sys


class easydriver(object):
    def __init__(self, delay=0.1, pin_step=0, pin_direction=0, pin_ms1=0, pin_ms2=0, pin_ms3=0, pin_enable=0):
        
        self.delay = delay / 2

        gpio.setmode(gpio.BCM)
        gpio.setwarnings(False)

        self.pin_step = pin_step
        gpio.setup(self.pin_step, gpio.OUT)
        
        self.pin_direction = pin_direction
        gpio.setup(self.pin_direction, gpio.OUT)
        gpio.output(self.pin_direction, True)

        # sixteenth step by defult 
        ##########
        self.pin_microstep_1 = pin_ms1
        gpio.setup(self.pin_microstep_1, gpio.OUT)
        gpio.output(self.pin_microstep_1, True)

        self.pin_microstep_2 = pin_ms2
        gpio.setup(self.pin_microstep_2, gpio.OUT)
        gpio.output(self.pin_microstep_2, True)

        self.pin_microstep_3 = pin_ms3
        gpio.setup(self.pin_microstep_3, gpio.OUT)
        gpio.output(self.pin_microstep_3, True)
        ##########

        self.pin_enable = pin_enable
        gpio.setup(self.pin_enable, gpio.OUT)
        gpio.output(self.pin_enable,False)
       


    def step(self, no_steps):
        
        for s in range ( no_steps ):
            gpio.output(self.pin_step,True)
            time.sleep(self.delay)
            gpio.output(self.pin_step,False)
            time.sleep(self.delay)

    def set_direction(self,direction):
        gpio.output(self.pin_direction,direction)

    def set_sixteenth_step(self):
        gpio.output(self.pin_microstep_1,True)
        gpio.output(self.pin_microstep_2,True)
        gpio.output(self.pin_microstep_3,True)

    def disable(self):
        gpio.output(self.pin_enable,True)

    def enable(self):
        gpio.output(self.pin_enable,False)

    def finish(self):
        gpio.cleanup()