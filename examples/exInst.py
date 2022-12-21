import init_examples

import sys
import os
import threading
import socket
import time

from ema_timber.communicate import Client, Server, Instructor, i_sProt
from ema_timber.communicate.Broadcast import tunein

def alive(BROADPORT,TYPE, ID, IP, PORT):
    delta  = "c"
    while True:
        # LISTEN TO BC
        T_HOST, T_PORT = tunein(BROADPORT, TYPE)
        if T_HOST and T_HOST:
            if delta != "a":
                print ("SERVER addr : ", T_HOST, T_PORT)
                # GIVE ADDR
                Client.PING(T_HOST, T_PORT, ID, IP, PORT, False)
            time.sleep(5)
            delta = "a"
        else:
        
            if delta !="b":
                print ("No connection to server")
            time.sleep(2)
            delta = "b"

def main():

    #+++++++++++++++++++++++++++++#
    ID = "99"
    TYPE = 0
    HOSTNAME = socket.gethostname()
    IP = socket.gethostbyname(HOSTNAME)
    PORT = 52344 #RECIVING DATA AT
    
    BROADPORT = 55555 #BROADCASTING 

    print (f"\nCLIENT SERVER : \n\t{HOSTNAME} - {IP}")
    print (f"PORTS : \n\tReceiving - {ID} : {PORT}\n")
    #+++++++++++++++++++++++++++++#

    i_s = i_sProt.getprgls()

    threads = []


    S = threading.Thread(
        target = Server,
        args = (IP, PORT, ID, i_s)
        )
    S.start()
    threads.append(S)

    ALIVE = threading.Thread(
        target = alive,
        args = (BROADPORT, TYPE, ID, IP, PORT)
    )
    ALIVE.start()
    threads.append(ALIVE)

    run = False

    while not run:
         # LISTEN TO BC
        T_HOST, T_PORT = tunein(BROADPORT, TYPE)
        if T_HOST and T_HOST:
            run = True
        else:
            time.sleep(2)



    operate = threading.Thread(
        target = Instructor,
        args= ( T_HOST, T_PORT, ID, i_s)
        )
    operate.start()
    threads.append(operate)

    for tt in threads:
        tt.join()
if __name__ == "__main__":
    main()