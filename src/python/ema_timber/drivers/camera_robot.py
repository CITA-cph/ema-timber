#!/usr/bin/env python

__author__ = "Tom Svilans"
__copyright__ = "Copyright 2023, Tom Svilans"
__credits__ = ["Tom Svilans", ]
__license__ = "MIT"
__version__ = "1.0.1"
__email__ = "tsvi@kglakademi.dk"
__status__ = "Development"

"""
This module interprets standard G-code and translates it into
commands for the photo taking machine.

Usage: myscript.py BAR1 BAR2
"""

import logging
import time
from pyeasydriver import easydriver as ed
#import capture

pos_x = 0
ratio = 45.35

step_delay = 1.0 / ratio / 2 / 32 # 1mm per second
step_delay = step_delay / 10

def distance_to_steps(d):
    return int(d * ratio)
    
def steps_to_distance(s):
    return float(s) / ratio

def line_to_tokens(line):
    '''
    Parse a string for G-code tokens
    '''
    tokens = []
    tok = ""
    in_quotes = False
    for c in line:
        if c.isspace(): continue
        if c == ';' and tok != "":
            tokens.append(tok)
            return tokens
        if not in_quotes and not c.isdigit() and tok != "":
            tokens.append(tok)
            tok = ""

        if c in ['\"', '\'']: 
            in_quotes = not in_quotes
            continue

        tok = tok + c
    if tok != "":
        tokens.append(tok)

    return tokens

def setupStepper():

    '''
    stepper = ed.EasyDriver(pin_step=18, delay=0.0005, pin_direction=23, pin_ms1=24, pin_ms2=17, pin_ms3=25)
    stepper.set_direction(True)
    stepper.set_sixteenth_step()

    return stepper
    '''
    pass
    
stepper = setupStepper()

def drive(stepper, distance):
    '''
    if distance < 0:
        stepper.set_direction(False)
    else:
        stepper.set_direction(True)

    for i in range(0, abs(distance)):
        stepper.step()
    '''

    pass

def move(tok):
    '''
    Moves the machine to the coordinates specified in a list of string tokens.

            Parameters:
                    tok (list): A list of string tokens.

            Returns:
                    result (int): 0 if successful, not 0 if not.
    '''
    global pos_x
    x = 0
    y = 0
    z = 0
    f = 1000

    for t in tok:
        if len(t) > 1:
            if t[0] == "X":
                x = float(t[1:])
            elif t[0] == "Y":
                y = float(t[1:])
            elif t[0] == "Z":
                z = float(t[1:])
            elif t[0] == "F":
                f = float(t[1:])

    if x > 4000 or x < 0:
        logging.critical(f"X position exceeds machine limits: {x:>7.2f}")
        return -1

    logging.debug(f"moving to {x:>7.2f} {y:>7.2f} {z:>7.2f} with speed {f:>5.0f}")
    steps = distance_to_steps(x)
    diff = steps - pos_x
    pos_x += diff
    
    drive(stepper, diff)

    return 0

def moveRapid(tok):
    '''
    Moves the machine to the coordinates specified in a list of string tokens at maximum speed.

            Parameters:
                    tok (list): A list of string tokens.

            Returns:
                    result (int): 0 if successful, not 0 if not.
    '''
    tok.append("F5000")
    return move(tok)

def capture(tok=None):
    '''
    Takes a photo.

            Parameters:
                    tok (list): A list of string tokens to capture extra input data.

            Returns:
                    result (int): 0 if successful, not 0 if not.
    '''
    photo_id = "null"
    if tok and len(tok) > 0:
        photo_id = tok[0]
        if len(tok) > 1:
            logging.debug(f"extra photo data: {tok[1:]}")

    logging.debug(f"taking photo (id: {photo_id}) ... ")

    time.sleep(0.5)
    logging.debug("... done.")

    return 0

def parse_gcode(gid, toks):
    if gid == 0:
        return moveRapid(toks)
    elif gid == 1:
        return move(toks)
    elif gid == 54:
        logging.debug(f"Setting G54 to {toks}")
        return 0

def parse_mcode(mid, toks):
    if mid == 5:
        return capture(toks)
    else:
        logging.debug(f"Unrecognized M-code {mid} with parameters {toks}")
        return 0

def execute(program):
    '''
    Executes a list of G-code commands on the machine.

            Parameters:
                    program (list): A list of strings, each representing a G-code command.

            Returns:
                    result (int): 0 if successful, not 0 if not.
    '''
    command = {
        "G": parse_gcode,
        "M": parse_mcode
        }

    for i, line in enumerate(program):
        tok = line_to_tokens(line)
        cmd_id = tok[0][0]
        #logging.info(f"{cmd_id}")
        if cmd_id in command.keys():
            res = command[cmd_id](int(tok[0][1:]), tok[1:])
            if res != 0:
                logging.critical(f"Critical error encountered at line {i}, command {tok[0]}: {res}")
                logging.critical(f"Aborting.")
                return res
        else:
            logging.warning(f"received invalid command: {tok[0]}")

if __name__ == "__main__":

    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    
    program = [
        "G0 X0", 
        "G54", 
        "M5", 
        "G1 X100 Z399.09079987", 
        "M5 \"Board04_123 test\"", 
        "B12", 
        "G1 X50 F200",
        "G0 X200",
        "G0 X0"
        ]

    tok = line_to_tokens(program[0])

    execute(program)
